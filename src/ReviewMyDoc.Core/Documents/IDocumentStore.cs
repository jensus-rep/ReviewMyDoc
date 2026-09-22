// What the document aggregate needs from persistence, said in terms of
// documents and sections instead of paths and content. It exists so that
// DocumentService can be read, and tested, without any knowledge of where a
// document lies or how it is spelled in a file.

using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// Reads and writes the entries of one document: its <c>document.json</c> and
/// the Markdown text of its sections.
/// </summary>
/// <remarks>
/// <para>
/// It sits between <see cref="DocumentService"/> and
/// <see cref="IObjectStore"/>. The service decides what a change means, this
/// store decides where it goes and how it is spelled, and the object store below
/// knows only entries and versions. That is what keeps the one place per
/// aggregate that builds paths - see <see cref="DocumentPaths"/> - out of every
/// service and every page.
/// </para>
/// <para>
/// The conditions and the results are the ones of the object store, passed
/// through unchanged, because assurance 1 of <c>docs/Datenmodell.md</c> is kept
/// down there where the check and the write cannot be separated. Nothing here
/// turns a conflict into an exception.
/// </para>
/// </remarks>
public interface IDocumentStore
{
    /// <summary>Reads one document with the version its entry carries.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The document and its version, or <see langword="null"/> if there is no
    /// such document. A missing document is an ordinary answer: a link may name
    /// one that was deleted.
    /// </returns>
    /// <exception cref="System.Text.Json.JsonException">
    /// The entry exists but is not a <c>document.json</c> of this model.
    /// </exception>
    Task<StoredDocument?> ReadAsync(DocumentIdentifier documentId, CancellationToken cancellationToken);

    /// <summary>Writes the <c>document.json</c> of one document.</summary>
    /// <param name="document">The complete new state; the entry is replaced whole.</param>
    /// <param name="condition">
    /// <see cref="WriteCondition.MustNotExist"/> for a document that is being
    /// created, <see cref="WriteCondition.MustMatch(ETag)"/> with the version the
    /// caller read for every later change.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new version, or <see cref="ObjectWriteResult.Conflict"/> if the condition did not hold.</returns>
    Task<ObjectWriteResult> WriteAsync(
        Document document,
        WriteCondition condition,
        CancellationToken cancellationToken);

    /// <summary>Reads every document there is.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// Every document, in no particular order. Empty if there is none, which is
    /// an ordinary answer and not a failure.
    /// </returns>
    /// <remarks>
    /// <c>docs/Datenmodell.md</c> answers the question "which documents are
    /// there" by listing the container by prefix instead of keeping a second
    /// file that names them, so this is the one place that turns the plain paths
    /// <see cref="Storage.IObjectStore.ListAsync"/> hands back into documents. It
    /// reads every <c>document.json</c> it finds; ordering the result for a page
    /// is not its job, because two different pages may want two different
    /// orders.
    /// </remarks>
    Task<IReadOnlyList<Document>> ListDocumentsAsync(CancellationToken cancellationToken);

    /// <summary>Writes the Markdown text of one section.</summary>
    /// <param name="documentId">Which document the section belongs to.</param>
    /// <param name="sectionId">Which section.</param>
    /// <param name="text">The complete text; empty is a real state, not a missing one.</param>
    /// <param name="condition">The condition the write has to satisfy.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new version, or <see cref="ObjectWriteResult.Conflict"/> if the condition did not hold.</returns>
    Task<ObjectWriteResult> WriteSectionTextAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        string text,
        WriteCondition condition,
        CancellationToken cancellationToken);

    /// <summary>Removes the Markdown text of one section.</summary>
    /// <param name="documentId">Which document the section belonged to.</param>
    /// <param name="sectionId">Which section.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// <see cref="ObjectDeleteResult.Deleted"/>, or
    /// <see cref="ObjectDeleteResult.NotFound"/> if there was no such text.
    /// </returns>
    /// <remarks>
    /// Carries no condition, as assurance 1 of <c>docs/Datenmodell.md</c> lays
    /// down: which sections a document has is decided in <c>document.json</c>,
    /// and removing the text of one that is no longer listed there is cleanup,
    /// not a race. Deleting twice stays without effect.
    /// </remarks>
    Task<ObjectDeleteResult> DeleteSectionTextAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        CancellationToken cancellationToken);
}

/// <summary>A document as it was read, together with the version its entry carried.</summary>
/// <param name="Document">The document.</param>
/// <param name="ETag">
/// The version at the moment of the read. Handing it back on the next write is
/// how assurance 1 of <c>docs/Datenmodell.md</c> is kept.
/// </param>
public sealed record StoredDocument(Document Document, ETag ETag);
