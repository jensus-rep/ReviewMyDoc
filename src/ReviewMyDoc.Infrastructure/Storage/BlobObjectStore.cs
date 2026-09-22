// The Azure half of the storage described in docs/Datenmodell.md: it maps the
// paths of the model straight onto blob names in one container and hands the
// conditions of a write to the service, which checks and writes them in one
// step. It exists because this is how the application runs in production, and
// because the assurances of the data model are cheaper and safer to keep here
// than anywhere else: the service already knows them.

using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ReviewMyDoc.Core.Storage;

// Two types are called ETag here: the one of the interface and the one of the
// Azure SDK. The alias gives the name to the interface, because that is the one
// this file hands out and takes in, and the few places that speak to the SDK
// name its type in full.
using ETag = ReviewMyDoc.Core.Storage.ETag;

namespace ReviewMyDoc.Infrastructure.Storage;

/// <summary>
/// Keeps the entries of <see cref="IObjectStore"/> as blobs in one Azure Blob
/// Storage container.
/// </summary>
/// <remarks>
/// <para>
/// The version stamp is the ETag of the blob, exactly as the service reports
/// it, and it goes back to the service as <c>If-Match</c>. That is the whole
/// reason the interface carries a condition instead of leaving the check to the
/// caller: the service checks the condition and writes in one operation, so no
/// second writer fits in between, and the window the local store leaves open
/// between reading and renaming does not exist here.
/// </para>
/// <para>
/// This class therefore deliberately does not imitate the local solution. It
/// never reads an entry in order to compare its version before writing, and it
/// holds no lock of its own: a check made here would be a worse copy of one the
/// service already makes, and it would reintroduce the very gap that
/// <c>If-Match</c> closes. Every condition of
/// <see cref="WriteCondition"/> maps to one header:
/// <see cref="WriteConditionKind.MustNotExist"/> to <c>If-None-Match: *</c>,
/// <see cref="WriteConditionKind.MustMatch"/> to <c>If-Match</c> with the
/// stamp of the read, and <see cref="WriteConditionKind.Unconditional"/> to no
/// header at all.
/// </para>
/// <para>
/// The audit log is an append blob and every other entry is a block blob. An
/// append blob is the one kind of blob to which a line can be added without
/// reading what is already there, which is what
/// <see cref="AppendLineAsync"/> promises; it comes into being on the first
/// append, so no caller has to check first.
/// </para>
/// <para>
/// The store expects the container to exist and never creates it. Creating it
/// would mean a network call on every start and a permission the application
/// does not otherwise need; <c>docs/Betrieb.md</c> says where the container
/// comes from instead. A missing container is a broken configuration and is
/// reported as <see cref="ObjectStoreException"/>, not as a missing entry.
/// </para>
/// </remarks>
public sealed class BlobObjectStore : IObjectStore
{
    /// <summary>
    /// UTF-8 without a byte order mark, as the interface demands, and the same
    /// encoding the local store uses. The mark would be an invisible character
    /// in front of every entry and would break the first JSON read.
    /// </summary>
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// The separator between two lines of the log: a single line feed and never
    /// the platform default, so the log reads the same no matter where the
    /// application runs, and the same one the local store writes.
    /// </summary>
    private const string LineSeparator = "\n";

    private readonly BlobContainerClient _container;

    /// <summary>Opens the store over one existing container.</summary>
    /// <param name="container">
    /// The container that holds the entries. It is handed in rather than built
    /// here, so the composition root decides how the application authenticates
    /// and the tests can point the store at a container of their own.
    /// </param>
    /// <exception cref="ArgumentNullException">No container was handed over.</exception>
    public BlobObjectStore(BlobContainerClient container)
    {
        ArgumentNullException.ThrowIfNull(container);

        _container = container;
    }

    /// <inheritdoc />
    public async Task<ObjectReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        ObjectPath.Validate(path, nameof(path));

        try
        {
            var download = await _container.GetBlobClient(path).DownloadContentAsync(cancellationToken);

            return new ObjectReadResult.Found(
                Utf8WithoutBom.GetString(download.Value.Content.ToArray()),
                ToETag(download.Value.Details.ETag));
        }
        catch (RequestFailedException exception) when (IsMissingBlob(exception))
        {
            return new ObjectReadResult.NotFound();
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw Failure($"The entry '{path}' could not be read.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<ObjectWriteResult> WriteAsync(
        string path,
        string content,
        WriteCondition condition,
        CancellationToken cancellationToken)
    {
        ObjectPath.Validate(path, nameof(path));
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(condition);

        var options = new BlobUploadOptions
        {
            Conditions = ConditionsFor(condition),
            HttpHeaders = new BlobHttpHeaders { ContentType = ContentTypeFor(path) },
        };

        try
        {
            // The stream is built over the bytes without copying them, and the
            // upload is one request: the whole content is replaced, never
            // patched, which is what the interface promises.
            using var payload = new MemoryStream(Utf8WithoutBom.GetBytes(content), writable: false);
            var written = await _container.GetBlobClient(path).UploadAsync(payload, options, cancellationToken);

            return new ObjectWriteResult.Written(ToETag(written.Value.ETag));
        }
        catch (RequestFailedException exception) when (IsConditionNotMet(exception, condition.Kind))
        {
            return new ObjectWriteResult.Conflict();
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw Failure($"The entry '{path}' could not be written.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken)
    {
        ObjectPath.ValidatePrefix(prefix, nameof(prefix));

        var paths = new List<string>();

        try
        {
            // The service matches the prefix by characters and not by path
            // segments, which is exactly what the interface promises, so the
            // filtering happens where the data is and not in this process.
            await foreach (var blob in _container
                .GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken)
                .ConfigureAwait(false))
            {
                paths.Add(blob.Name);
            }
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw Failure($"The entries below '{prefix}' could not be listed.", exception);
        }

        // The service already lists in lexicographical order, but the order the
        // interface promises is an ordinal one and is sorted here rather than
        // assumed: it costs nothing at these sizes and it is the same line of
        // code that gives the local store its order.
        paths.Sort(StringComparer.Ordinal);

        return paths;
    }

    /// <inheritdoc />
    public async Task AppendLineAsync(string path, string line, CancellationToken cancellationToken)
    {
        ObjectPath.Validate(path, nameof(path));
        ArgumentNullException.ThrowIfNull(line);

        // Checked before anything is created: a rejected line must not leave an
        // entry behind, and a carriage return would break the JSON line as
        // thoroughly as a line feed.
        if (line.Contains('\n') || line.Contains('\r'))
        {
            throw new ArgumentException(
                "A line of the log carries no line break; the store adds the separator.",
                nameof(line));
        }

        var blob = _container.GetAppendBlobClient(path);

        try
        {
            // Creating and appending are two requests, and they may not be. Two
            // callers that record their first event at the same moment both
            // create, and the second create is refused by the service and
            // swallowed here, because an append blob that is already there is
            // precisely the state this call wants. Nothing is lost: the create
            // carries If-None-Match and therefore never empties an existing log.
            await blob.CreateIfNotExistsAsync(
                new AppendBlobCreateOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = ContentTypeFor(path) },
                },
                cancellationToken);

            using var block = new MemoryStream(
                Utf8WithoutBom.GetBytes(line + LineSeparator),
                writable: false);

            // One append is one request and the service writes it whole, so two
            // events can never land inside one another. That is the reason
            // appending is an operation of its own rather than read, extend and
            // write back.
            await blob.AppendBlockAsync(block, options: null, cancellationToken);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw Failure($"The line could not be appended to '{path}'.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<ObjectDeleteResult> DeleteAsync(string path, CancellationToken cancellationToken)
    {
        ObjectPath.Validate(path, nameof(path));

        try
        {
            // Deliberately not DeleteIfExistsAsync: that call also swallows a
            // missing container and would report a broken configuration as an
            // entry that simply was not there. Only a missing blob is an
            // ordinary answer here.
            await _container.GetBlobClient(path).DeleteAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                conditions: null,
                cancellationToken);

            return ObjectDeleteResult.Deleted;
        }
        catch (RequestFailedException exception) when (IsMissingBlob(exception))
        {
            return ObjectDeleteResult.NotFound;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw Failure($"The entry '{path}' could not be deleted.", exception);
        }
    }

    /// <summary>Turns the condition of the interface into the headers of the service.</summary>
    /// <remarks>
    /// This is the single place where a
    /// <see cref="WriteCondition"/> becomes an <c>If-Match</c> or an
    /// <c>If-None-Match</c>, which is what
    /// <c>docs/Konventionen.md</c>, section Code, asks for.
    /// </remarks>
    private static BlobRequestConditions? ConditionsFor(WriteCondition condition) => condition.Kind switch
    {
        // If-None-Match: * lets the write through only if there is no blob yet,
        // which is assurance 2 of docs/Datenmodell.md for versions/{n}.json.
        WriteConditionKind.MustNotExist => new BlobRequestConditions { IfNoneMatch = Azure.ETag.All },

        // If-Match carries the stamp of the read, which is assurance 1.
        WriteConditionKind.MustMatch => new BlobRequestConditions
        {
            IfMatch = new Azure.ETag(condition.ExpectedETag!.Value),
        },

        // Nothing is demanded, so nothing is sent and the blob is replaced.
        _ => null,
    };

    /// <summary>
    /// Says whether the service refused a write because the condition it
    /// carried did not hold, which is the conflict the interface reports.
    /// </summary>
    /// <remarks>
    /// The answers differ by condition, and each one is claimed only for the
    /// condition that can produce it, so an unexpected status still becomes an
    /// <see cref="ObjectStoreException"/> instead of a silent conflict. An
    /// unconditional write can produce none of them.
    /// </remarks>
    private static bool IsConditionNotMet(RequestFailedException exception, WriteConditionKind kind) => kind switch
    {
        // The blob was already there. The service answers 409 to
        // If-None-Match: * and 412 where the condition is evaluated as a
        // precondition, and both mean the same thing here.
        WriteConditionKind.MustNotExist =>
            HasErrorCode(exception, BlobErrorCode.BlobAlreadyExists)
            || HasErrorCode(exception, BlobErrorCode.ConditionNotMet),

        // Either the stamp no longer fits, or the blob is gone. A blob that was
        // deleted in the meantime is a conflict too, because writing it again
        // would undo that delete.
        WriteConditionKind.MustMatch =>
            HasErrorCode(exception, BlobErrorCode.ConditionNotMet)
            || IsMissingBlob(exception),

        _ => false,
    };

    /// <summary>Says whether the service reported that this one blob is not there.</summary>
    /// <remarks>
    /// A missing container answers 404 as well and is deliberately not counted:
    /// it is a broken configuration and belongs in the log as such, not in an
    /// empty read result that every caller would take for an empty document.
    /// </remarks>
    private static bool IsMissingBlob(RequestFailedException exception) =>
        HasErrorCode(exception, BlobErrorCode.BlobNotFound);

    /// <summary>Compares the error code of a failure with one the service defines.</summary>
    private static bool HasErrorCode(RequestFailedException exception, BlobErrorCode code) =>
        string.Equals(exception.ErrorCode, code.ToString(), StringComparison.Ordinal);

    /// <summary>
    /// Tells the failures the store answers for from the ones that belong to
    /// the caller or to the runtime, such as a cancellation.
    /// </summary>
    /// <remarks>
    /// A cancelled operation reaches the caller as the
    /// <see cref="OperationCanceledException"/> it asked for, and a mistake of
    /// this class stays a mistake of this class; everything the service and the
    /// sign-in can report is turned into
    /// <see cref="ObjectStoreException"/> by the caller of this method.
    /// </remarks>
    private static bool IsStorageFailure(Exception exception) =>
        exception is RequestFailedException or Azure.Identity.AuthenticationFailedException;

    /// <summary>
    /// Wraps a failure of the service, so no type of Azure leaves this class.
    /// </summary>
    /// <remarks>
    /// The message names what was attempted, in the terms of the store, and the
    /// original failure hangs inside it for the log. Nothing is added to the
    /// message that a log must not hold: no token, no account name and no
    /// personal data. See <c>docs/Konventionen.md</c>, section Code.
    /// </remarks>
    private static ObjectStoreException Failure(string message, Exception exception) =>
        new(message, exception);

    /// <summary>The version stamp of the interface, taken from the one of the service.</summary>
    /// <remarks>
    /// Written in the form the HTTP header uses, which is the form the value
    /// goes back in as <c>If-Match</c>. Choosing one form and keeping it means
    /// a stamp read today still fits a write tomorrow, no matter which version
    /// of the SDK writes it.
    /// </remarks>
    private static ETag ToETag(Azure.ETag etag) => new(etag.ToString("H"));

    /// <summary>
    /// The media type an entry is stored with, so a blob opened in the portal or
    /// in a browser is shown as the text it is instead of being offered as a
    /// download. It carries no meaning for the store and is never read back.
    /// </summary>
    private static string ContentTypeFor(string path) => Path.GetExtension(path) switch
    {
        ".json" => "application/json; charset=utf-8",
        ".md" => "text/markdown; charset=utf-8",
        _ => "text/plain; charset=utf-8",
    };
}
