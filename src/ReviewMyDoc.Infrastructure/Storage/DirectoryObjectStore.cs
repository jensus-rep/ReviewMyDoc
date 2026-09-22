// The local half of the storage described in docs/Datenmodell.md: it maps the
// paths of the model onto a directory tree, imitates the ETag semantics of blob
// storage by hashing the content, and writes every entry through a side file so
// an interrupted write leaves no half entry behind. It exists so the
// application can be run and tested without Azure, and because every test of
// this project measures the store against this implementation.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Infrastructure.Storage;

/// <summary>
/// Keeps the entries of <see cref="IObjectStore"/> as files below one root
/// directory, in practice a directory under <c>App_Data/</c>.
/// </summary>
/// <remarks>
/// <para>
/// The version stamp is the SHA-256 of the content, written as lower case hex.
/// A file system has no version of its own, and a timestamp would be both too
/// coarse and dependent on the clock, while the hash is exactly what assurance
/// 1 of <c>docs/Datenmodell.md</c> needs: it changes when the content changes,
/// and it is the same on every read. That two entries with equal content carry
/// the same stamp is not a defect; the interface promises an opaque value that
/// is compared ordinally and nothing more, and nobody compares the stamps of
/// two different entries.
/// </para>
/// <para>
/// Every write goes into a side file next to the target and is then renamed
/// onto it. A rename inside one directory is a single step of the file system,
/// so a reader sees either the old entry or the new one; a write that is
/// interrupted leaves a side file behind and never a half entry. Side files
/// begin with a dot, which no path of the model may do, so a leftover is
/// invisible to <see cref="ListAsync"/> and can be removed by hand at any time.
/// Durability beyond that - surviving a power cut - is not promised here; that
/// is what the Azure implementation is for.
/// </para>
/// <para>
/// What the condition of a write achieves, and what it does not, is written
/// down at <see cref="WriteUnderGateAsync"/>.
/// </para>
/// </remarks>
public sealed class DirectoryObjectStore : IObjectStore
{
    /// <summary>
    /// UTF-8 without a byte order mark, as the interface demands. The mark
    /// would be an invisible character in front of every entry and would break
    /// the first JSON read.
    /// </summary>
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// One gate per entry, shared by every store of this process. It makes the
    /// check and the write of a conditional write one step as far as one
    /// process is concerned; see <see cref="WriteUnderGateAsync"/> for the
    /// limits. The key is the resolved file name and is compared without regard
    /// to case, because a Windows file system does the same and two names that
    /// differ only in case are one file there. Gates are never removed: there
    /// is one per entry this process has touched, which is a few hundred bytes
    /// for a local store, and removing one that another writer is about to take
    /// would bring back the very race this guards against.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Side files carry this prefix. It is the very character
    /// <see cref="ObjectPath"/> refuses at the start of a path segment, which
    /// is what makes a side file invisible to <see cref="ListAsync"/> and
    /// impossible to address as an entry.
    /// </summary>
    private const string SideFilePrefix = ObjectPath.DotPrefix;

    /// <summary>How often an append waits for a writer of another process.</summary>
    private const int AppendAttempts = 10;

    /// <summary>The size of the buffer every file operation of this store works with.</summary>
    private const int BufferSize = 4096;

    /// <summary>How long an append waits between two attempts.</summary>
    private static readonly TimeSpan AppendDelay = TimeSpan.FromMilliseconds(25);

    private readonly string _root;
    private readonly string _rootWithSeparator;

    /// <summary>Opens the store over one directory and creates it if it is not there.</summary>
    /// <param name="rootPath">
    /// The directory that holds the entries. A relative path is resolved
    /// against the working directory, so the caller decides where
    /// <c>App_Data/</c> lies.
    /// </param>
    /// <exception cref="ArgumentException">No directory was named.</exception>
    /// <exception cref="ObjectStoreException">The directory could not be created.</exception>
    public DirectoryObjectStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        _root = Path.GetFullPath(rootPath);
        _rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        try
        {
            Directory.CreateDirectory(_root);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new ObjectStoreException("The directory of the object store could not be created.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<ObjectReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        ObjectPath.Validate(path, nameof(path));

        var file = Resolve(path, nameof(path));

        try
        {
            // The entry stays open for writing and deleting while it is read: a
            // reader that kept a writer out would make the rename of a write
            // fail on Windows, and reading an entry that is replaced a moment
            // later is exactly what the version stamp is there to report.
            await using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);

            var content = Utf8WithoutBom.GetString(buffer.ToArray());

            return new ObjectReadResult.Found(content, ComputeETag(content));
        }
        catch (FileNotFoundException)
        {
            return new ObjectReadResult.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return new ObjectReadResult.NotFound();
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new ObjectStoreException($"The entry '{path}' could not be read.", exception);
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

        var file = Resolve(path, nameof(path));
        var gate = GateFor(file);

        await gate.WaitAsync(cancellationToken);
        try
        {
            return await WriteUnderGateAsync(file, path, content, condition, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken)
    {
        ObjectPath.ValidatePrefix(prefix, nameof(prefix));
        cancellationToken.ThrowIfCancellationRequested();

        // Everything the prefix can match lies below the directory it names in
        // full, so the walk starts there instead of at the root. The rest of
        // the prefix is compared character by character, as the interface
        // demands: a prefix that ends in the middle of a name matches too.
        var lastSeparator = prefix.LastIndexOf('/');
        var directoryPart = lastSeparator < 0 ? string.Empty : prefix[..(lastSeparator + 1)];
        var startDirectory = directoryPart.Length == 0 ? _root : Resolve(directoryPart, nameof(prefix));

        try
        {
            if (!Directory.Exists(startDirectory))
            {
                return Task.FromResult<IReadOnlyList<string>>([]);
            }

            var paths = new List<string>();
            foreach (var file in Directory.EnumerateFiles(startDirectory, "*", SearchOption.AllDirectories))
            {
                // A side file of a write that was interrupted is not an entry.
                if (Path.GetFileName(file).StartsWith(SideFilePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var candidate = Path.GetRelativePath(_root, file).Replace(Path.DirectorySeparatorChar, '/');
                if (candidate.StartsWith(prefix, StringComparison.Ordinal))
                {
                    paths.Add(candidate);
                }
            }

            paths.Sort(StringComparer.Ordinal);

            return Task.FromResult<IReadOnlyList<string>>(paths);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new ObjectStoreException($"The entries below '{prefix}' could not be listed.", exception);
        }
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

        var file = Resolve(path, nameof(path));

        // The separator is a single line feed and never the platform default,
        // so the log reads the same no matter where the application runs. It
        // follows the line instead of preceding it, which spares the append a
        // read of what is already there.
        var bytes = Utf8WithoutBom.GetBytes(line + "\n");
        var gate = GateFor(file);

        await gate.WaitAsync(cancellationToken);
        try
        {
            CreateParentDirectory(file, path);
            await AppendUnderGateAsync(file, path, bytes, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ObjectDeleteResult> DeleteAsync(string path, CancellationToken cancellationToken)
    {
        ObjectPath.Validate(path, nameof(path));

        var file = Resolve(path, nameof(path));
        var gate = GateFor(file);

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(file))
            {
                return ObjectDeleteResult.NotFound;
            }

            File.Delete(file);

            return ObjectDeleteResult.Deleted;
        }
        catch (DirectoryNotFoundException)
        {
            return ObjectDeleteResult.NotFound;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new ObjectStoreException($"The entry '{path}' could not be deleted.", exception);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Writes one entry while the gate of that entry is held.</summary>
    /// <remarks>
    /// <para>
    /// What each condition is worth here.
    /// <see cref="WriteConditionKind.MustNotExist"/> is kept by the file system
    /// itself: the side file is renamed onto the target without permission to
    /// replace it, so either the rename succeeds and the entry was new, or it
    /// fails and the entry was already there. Nobody fits in between, not even
    /// another process, which is what makes assurance 2 of
    /// <c>docs/Datenmodell.md</c> hold.
    /// <see cref="WriteConditionKind.Unconditional"/> demands nothing and needs
    /// no protection.
    /// </para>
    /// <para>
    /// <see cref="WriteConditionKind.MustMatch"/> is the case a file system
    /// cannot keep on its own, because there is no rename that first compares
    /// what it replaces. The check and the rename are therefore made one step
    /// by the gate above, and that gate holds for every writer inside this
    /// process: two browser tabs of the local application cannot lose an
    /// update. A writer in a second process is not held by it. Between reading
    /// the current content and the rename there remains a window in which that
    /// writer could put its own version in place, which this write would then
    /// replace without reporting a conflict. Both ways of narrowing the window
    /// further were rejected: a lock file would bind only the processes that
    /// use this class, and moving the target aside in order to inspect it
    /// exclusively would leave no entry at all if the process died in between,
    /// which is worse than the window it closes. The local store is the store
    /// of one machine and one process; where several writers really meet, the
    /// application runs against Azure, whose If-Match closes the window inside
    /// the storage.
    /// </para>
    /// </remarks>
    private async Task<ObjectWriteResult> WriteUnderGateAsync(
        string file,
        string path,
        string content,
        WriteCondition condition,
        CancellationToken cancellationToken)
    {
        if (condition.Kind == WriteConditionKind.MustMatch)
        {
            var current = await ReadAsync(path, cancellationToken);
            if (current is not ObjectReadResult.Found found || found.ETag != condition.ExpectedETag)
            {
                // A missing entry is a conflict too: it was deleted in the
                // meantime, and writing it again would undo that delete.
                return new ObjectWriteResult.Conflict();
            }
        }

        CreateParentDirectory(file, path);

        var sideFile = await WriteSideFileAsync(file, content, path, cancellationToken);

        try
        {
            File.Move(sideFile, file, overwrite: condition.Kind != WriteConditionKind.MustNotExist);
        }
        catch (IOException) when (condition.Kind == WriteConditionKind.MustNotExist && File.Exists(file))
        {
            // The rename refused to replace an entry, which is exactly the
            // condition that was asked for.
            DeleteSideFile(sideFile);

            return new ObjectWriteResult.Conflict();
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            DeleteSideFile(sideFile);

            throw new ObjectStoreException($"The entry '{path}' could not be written.", exception);
        }

        return new ObjectWriteResult.Written(ComputeETag(content));
    }

    /// <summary>Writes the content into a side file beside the target and closes it.</summary>
    private static async Task<string> WriteSideFileAsync(
        string file,
        string content,
        string path,
        CancellationToken cancellationToken)
    {
        var sideFile = Path.Combine(
            Path.GetDirectoryName(file)!,
            SideFilePrefix + Guid.NewGuid().ToString("n") + ".tmp");

        try
        {
            await using var stream = new FileStream(
                sideFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous);
            await stream.WriteAsync(Utf8WithoutBom.GetBytes(content), cancellationToken);
            await stream.FlushAsync(cancellationToken);

            return sideFile;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            DeleteSideFile(sideFile);

            throw new ObjectStoreException($"The entry '{path}' could not be written.", exception);
        }
    }

    /// <summary>Appends the bytes, waiting for a writer of another process.</summary>
    /// <remarks>
    /// The entry is opened for writing without sharing that right, so a second
    /// appender is kept out until this one is done and no event can land in the
    /// middle of another. Reading stays possible throughout. An appender of
    /// another process makes the open fail, which is why it is tried again for
    /// a short while instead of being reported as a failure at once.
    /// </remarks>
    private static async Task AppendUnderGateAsync(
        string file,
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var stream = new FileStream(
                    file,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    BufferSize,
                    FileOptions.Asynchronous);
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                return;
            }
            catch (IOException exception) when (attempt < AppendAttempts && IsSharingViolation(exception))
            {
                await Task.Delay(AppendDelay, cancellationToken);
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            {
                throw new ObjectStoreException($"The line could not be appended to '{path}'.", exception);
            }
        }
    }

    /// <summary>Creates the directory an entry lives in.</summary>
    private static void CreateParentDirectory(string file, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new ObjectStoreException(
                $"The directory for the entry '{path}' could not be created.",
                exception);
        }
    }

    /// <summary>
    /// Removes a side file whose content never reached the target. A failure
    /// here is passed over on purpose: the side file is invisible to the store,
    /// and an exception would cover up the failure that led to the cleanup.
    /// </summary>
    private static void DeleteSideFile(string sideFile)
    {
        try
        {
            File.Delete(sideFile);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            // The next write draws a fresh name, so there is nothing to do.
        }
    }

    /// <summary>Hands out the gate that belongs to one entry.</summary>
    private static SemaphoreSlim GateFor(string file) =>
        Gates.GetOrAdd(file, _ => new SemaphoreSlim(initialCount: 1, maxCount: 1));

    /// <summary>The version stamp of a content: the lower case hex of its SHA-256.</summary>
    private static ETag ComputeETag(string content) =>
        new(Convert.ToHexStringLower(SHA256.HashData(Utf8WithoutBom.GetBytes(content))));

    /// <summary>Turns a path of the model into a file name below the root.</summary>
    private string Resolve(string path, string parameterName)
    {
        var file = Path.GetFullPath(Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar)));

        // The validation above already refuses everything that could lead out
        // of the root, and this is the second lock on the same door: the cost
        // of being wrong here is a read or a write outside the store, so no
        // path reaches the file system without passing it.
        if (!file.StartsWith(_rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The path '{path}' leads out of the store.", parameterName);
        }

        return file;
    }

    /// <summary>
    /// Tells the failures the store answers for from the ones that belong to
    /// the caller or to the runtime, such as a cancellation.
    /// </summary>
    private static bool IsStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or NotSupportedException;

    /// <summary>
    /// Recognises the two Windows errors that say another process holds the
    /// file. On a system without mandatory locking they never appear and the
    /// append succeeds on its first attempt.
    /// </summary>
    private static bool IsSharingViolation(IOException exception) =>
        (exception.HResult & 0xFFFF) is 32 or 33;
}
