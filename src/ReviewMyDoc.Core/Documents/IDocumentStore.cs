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

    /// <summary>Reads the Markdown text of one section, with the version stamp it carries.</summary>
    /// <param name="documentId">Which document the section belongs to.</param>
    /// <param name="sectionId">Which section.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The text and its version, or <see langword="null"/> if there is no such entry.</returns>
    /// <remarks>
    /// The version stamp travels with it, unlike a plain read of content alone
    /// would give: <see cref="DocumentService.SplitSectionAsync"/> reads a
    /// section's text and, for the part that keeps the section's identifier,
    /// writes a shortened version of the very same entry back - the one place in
    /// this model that overwrites an existing section text rather than only ever
    /// adding one under a fresh identifier or reading it for comparison. Freezing
    /// a state and comparing against one still only ever read the content and
    /// leave the stamp unused, which is why it is optional to use and not a
    /// second required round trip.
    /// </remarks>
    Task<StoredSectionText?> ReadSectionTextAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        CancellationToken cancellationToken);

    /// <summary>Writes the <c>versions/{version}.json</c> of one frozen state.</summary>
    /// <param name="version">The frozen state to write.</param>
    /// <param name="condition">
    /// The condition the write has to satisfy;
    /// <see cref="WriteCondition.MustNotExist"/> for every ordinary freeze, as
    /// assurance 2 of <c>docs/Datenmodell.md</c> demands.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new version, or <see cref="ObjectWriteResult.Conflict"/> if the condition did not hold.</returns>
    Task<ObjectWriteResult> WriteVersionAsync(
        DocumentVersion version,
        WriteCondition condition,
        CancellationToken cancellationToken);

    /// <summary>Reads one frozen state of a document.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="version">Which version.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The frozen state, or <see langword="null"/> if this document was never
    /// frozen at that version - an ordinary answer, for example a stale link.
    /// </returns>
    Task<DocumentVersion?> ReadVersionAsync(
        DocumentIdentifier documentId,
        int version,
        CancellationToken cancellationToken);

    /// <summary>Removes one frozen state.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="version">Which version.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see cref="ObjectDeleteResult.Deleted"/>, or <see cref="ObjectDeleteResult.NotFound"/>.</returns>
    /// <remarks>
    /// Not a way to undo assurance 2 of <c>docs/Datenmodell.md</c>: the only
    /// caller is <see cref="DocumentService.FreezeVersionAsync"/>, and only for a
    /// state that <c>document.json</c> never ended up naming, because the write
    /// that would have made it official failed. Once a version is recorded in
    /// <c>document.json</c>, nothing in this model calls this again for it.
    /// </remarks>
    Task<ObjectDeleteResult> DeleteVersionAsync(
        DocumentIdentifier documentId,
        int version,
        CancellationToken cancellationToken);
}

/// <summary>A document as it was read, together with the version its entry carried.</summary>
/// <param name="Document">The document.</param>
/// <param name="ETag">
/// The version at the moment of the read. Handing it back on the next write is
/// how assurance 1 of <c>docs/Datenmodell.md</c> is kept.
/// </param>
public sealed record StoredDocument(Document Document, ETag ETag);

/// <summary>The Markdown text of one section as it was read, together with the version its entry carried.</summary>
/// <param name="Content">The text.</param>
/// <param name="ETag">
/// The version at the moment of the read. A caller that goes on to write a
/// changed version of the very same entry hands this back on
/// <see cref="WriteCondition.MustMatch(ETag)"/>, which is assurance 1 of
/// <c>docs/Datenmodell.md</c> kept for a section's text exactly as it is kept
/// for <c>document.json</c> through <see cref="StoredDocument"/>.
/// </param>
public sealed record StoredSectionText(string Content, ETag ETag);
