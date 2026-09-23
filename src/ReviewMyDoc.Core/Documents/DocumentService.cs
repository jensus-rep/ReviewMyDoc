// Stable document API; cohesive operation classes own outline, split and version workflows.
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Documents;

/// <summary>Coordinates document operations without owning their implementations.</summary>
public sealed class DocumentService : DocumentOperations
{
    private readonly TimeProvider _clock;
    /// <summary>Builds the document facade with the shared store and clock.</summary>
    public DocumentService(IDocumentStore store, TimeProvider clock) : base(store, clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <summary>Creates a document with a title and no sections.</summary>
    /// <param name="ownerId">Who the document belongs to.</param>
    /// <param name="title">The title the owner gave it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// <see cref="DocumentResult.Success"/> with the new document, or
    /// <see cref="DocumentResult.Conflict"/> in the case described in the
    /// remarks.
    /// </returns>
    /// <exception cref="ArgumentException">The owner or the title is empty.</exception>
    /// <remarks>
    /// Written with <see cref="WriteCondition.MustNotExist"/>, so a drawn
    /// identifier that against all expectation already names a document cannot
    /// overwrite it. That is the only way this operation reports a conflict, and
    /// the answer to it is to try again, not to reload anything.
    /// </remarks>
    public async Task<DocumentResult> CreateDocumentAsync(
        string ownerId,
        string title,
        CancellationToken cancellationToken)
    {
        var document = Document.Create(DocumentIdentifier.Draw(), ownerId, title, Now());

        var written = await _store.WriteAsync(document, WriteCondition.MustNotExist, cancellationToken);

        return written is ObjectWriteResult.Written created
            ? new DocumentResult.Success(document, created.ETag)
            : new DocumentResult.Conflict();
    }

    /// <summary>Lists every document, newest change first.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every document there is, sorted by <see cref="Document.UpdatedAt"/> descending.</returns>
    /// <remarks>
    /// The sort is the one rule of this operation and the reason it is not left
    /// to the page: whoever asks for the list of documents wants the one that
    /// changed most recently first, and stating that once here means every page
    /// that shows the list agrees on it without repeating it.
    /// </remarks>
    public async Task<IReadOnlyList<Document>> ListDocumentsAsync(CancellationToken cancellationToken)
    {
        var documents = await _store.ListDocumentsAsync(cancellationToken);

        return [.. documents.OrderByDescending(document => document.UpdatedAt)];
    }

    /// <summary>Loads a document with the version needed for the next change.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// <see cref="DocumentResult.Success"/>, or
    /// <see cref="DocumentResult.DocumentNotFound"/>.
    /// </returns>
    public async Task<DocumentResult> LoadDocumentAsync(
        DocumentIdentifier documentId,
        CancellationToken cancellationToken)
    {
        var stored = await _store.ReadAsync(documentId, cancellationToken);

        return stored is null
            ? new DocumentResult.DocumentNotFound()
            : new DocumentResult.Success(stored.Document, stored.ETag);
    }

    /// <summary>Gives a document another title.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="title">The new title.</param>
    /// <param name="expectedETag">The version the caller read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, <see cref="DocumentResult.DocumentNotFound"/> or <see cref="DocumentResult.Conflict"/>.</returns>
    /// <exception cref="ArgumentException">The title is empty.</exception>
    public async Task<DocumentResult> RenameDocumentAsync(
        DocumentIdentifier documentId,
        string title,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        var (stored, refused) = await LoadForChangeAsync(documentId, expectedETag, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        var changed = stored!.Document.Rename(title, Now());

        return await WriteChangeAsync(changed, expectedETag, cancellationToken);
    }

    /// <summary>Delegates to the operation that owns this change.</summary>
    public Task<DocumentResult> AddSectionAsync(
        DocumentIdentifier documentId,
        string heading,
        ETag expectedETag,
        CancellationToken cancellationToken) =>
        new DocumentOutlineService(_store, _clock).AddSectionAsync(documentId, heading, expectedETag, cancellationToken);

    /// <summary>Delegates to the operation that owns this change.</summary>
    public Task<DocumentResult> RenameSectionAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        string heading,
        ETag expectedETag,
        CancellationToken cancellationToken) =>
        new DocumentOutlineService(_store, _clock).RenameSectionAsync(documentId, sectionId, heading, expectedETag, cancellationToken);

    /// <summary>Delegates to the operation that owns this change.</summary>
    public Task<DocumentResult> ReorderSectionsAsync(
        DocumentIdentifier documentId,
        IReadOnlyList<SectionIdentifier> orderedSectionIds,
        ETag expectedETag,
        CancellationToken cancellationToken) =>
        new DocumentOutlineService(_store, _clock).ReorderSectionsAsync(documentId, orderedSectionIds, expectedETag, cancellationToken);

    /// <summary>Delegates to the operation that owns this change.</summary>
    public Task<DocumentResult> DeleteSectionAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        ETag expectedETag,
        CancellationToken cancellationToken) =>
        new DocumentOutlineService(_store, _clock).DeleteSectionAsync(documentId, sectionId, expectedETag, cancellationToken);

    /// <summary>Delegates to the operation that owns this change.</summary>
    public Task<DocumentResult> SplitSectionAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        int markStart,
        int markEnd,
        string heading,
        ETag expectedETag,
        CancellationToken cancellationToken) =>
        new DocumentSplitService(_store, _clock).SplitSectionAsync(documentId, sectionId, markStart, markEnd, heading, expectedETag, cancellationToken);

    /// <summary>Delegates to the operation that owns this change.</summary>
    public Task<DocumentResult> FreezeVersionAsync(
        DocumentIdentifier documentId,
        ETag expectedETag,
        CancellationToken cancellationToken) =>
        new DocumentVersionService(_store, _clock).FreezeVersionAsync(documentId, expectedETag, cancellationToken);

    /// <summary>Delegates to the operation that owns this change.</summary>
    public Task<VersionComparisonResult> FindChangedSectionsAsync(
        DocumentIdentifier documentId,
        int version,
        CancellationToken cancellationToken) =>
        new DocumentVersionService(_store, _clock).FindChangedSectionsAsync(documentId, version, cancellationToken);

}
