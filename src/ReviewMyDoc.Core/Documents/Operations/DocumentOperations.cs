// Shared optimistic-write boundary for document operations.
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Documents;

/// <summary>Storage and concurrency primitives shared by cohesive document operations.</summary>
public abstract class DocumentOperations
{
    /// <summary>The aggregate store.</summary>
    protected readonly IDocumentStore _store;
    private readonly TimeProvider _timeProvider;
    /// <summary>Connects operations to their persistence and clock.</summary>
    protected DocumentOperations(IDocumentStore store, TimeProvider clock) { _store = store; _timeProvider = clock; }
    /// <summary>
    /// Reads the document for a change and refuses it if there is nothing to
    /// change or the caller is working from an older state.
    /// </summary>
    /// <returns>
    /// Either the document that was read, or the result the operation has to
    /// return instead; exactly one of the two is set.
    /// </returns>
    protected async Task<(StoredDocument? Stored, DocumentResult? Refused)> LoadForChangeAsync(
        DocumentIdentifier documentId,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(expectedETag);

        var stored = await _store.ReadAsync(documentId, cancellationToken);
        if (stored is null)
        {
            return (null, new DocumentResult.DocumentNotFound());
        }

        if (stored.ETag != expectedETag || stored.Document.State == DocumentState.Approved)
        {
            return (null, new DocumentResult.Conflict());
        }

        return (stored, null);
    }

    /// <summary>Writes a changed document under the version the caller read.</summary>
    /// <remarks>
    /// The condition is the caller's version and not the one that was just read,
    /// although at this point they are equal: it is the version the user actually
    /// saw, and it is the one that has to hold at the moment of the write.
    /// </remarks>
    protected async Task<DocumentResult> WriteChangeAsync(
        Document changed,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        var written = await _store.WriteAsync(
            changed,
            WriteCondition.MustMatch(expectedETag),
            cancellationToken);

        return written is ObjectWriteResult.Written updated
            ? new DocumentResult.Success(changed, updated.ETag)
            : new DocumentResult.Conflict();
    }

    /// <summary>
    /// The current moment in UTC, cut to whole seconds.
    /// </summary>
    /// <remarks>
    /// <c>docs/Datenmodell.md</c> writes its timestamps to the second, and
    /// nothing in this model tells finer moments apart: the outline is shown with
    /// a date, and the order of two changes is decided by the version of an
    /// entry and not by a clock. Cutting here rather than when the file is
    /// written keeps the document in memory and the document on disk the same
    /// down to the last digit.
    /// </remarks>
    protected DateTimeOffset Now()
    {
        var now = _timeProvider.GetUtcNow();

        return new DateTimeOffset(now.UtcTicks - (now.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }
}
