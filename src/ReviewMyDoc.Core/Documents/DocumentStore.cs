// The one implementation of IDocumentStore. It maps a document onto the entries
// of docs/Datenmodell.md and back: which path, which text, which version. It
// holds no rule of its own - what may be changed and when is decided in
// DocumentService, what a document looks like in Document.

using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// Stores documents as the entries <c>docs/Datenmodell.md</c> describes, through
/// an <see cref="IObjectStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// It lives in <c>ReviewMyDoc.Core</c> and not in <c>ReviewMyDoc.Infrastructure</c>
/// because it names nothing of any storage: it turns documents into text and
/// paths and hands both to the object store, whose two implementations are the
/// ones that know Azure and the file system. <c>docs/Konventionen.md</c>, section
/// Struktur, places the document aggregate here for exactly that reason, and the
/// single place per aggregate that builds paths, <see cref="DocumentPaths"/>, is
/// beside it.
/// </para>
/// <para>
/// Every method is a translation and nothing more. A conflict travels up
/// untouched, so the decision what to tell the user is taken in one place and not
/// spread over the layers.
/// </para>
/// </remarks>
public sealed class DocumentStore : IDocumentStore
{
    private readonly IObjectStore _objects;

    /// <summary>Builds the store over the object store the application is configured with.</summary>
    /// <param name="objects">Where the entries live.</param>
    /// <exception cref="ArgumentNullException">No object store was handed over.</exception>
    public DocumentStore(IObjectStore objects)
    {
        ArgumentNullException.ThrowIfNull(objects);

        _objects = objects;
    }

    /// <inheritdoc />
    public async Task<StoredDocument?> ReadAsync(
        DocumentIdentifier documentId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentId);

        var entry = await _objects.ReadAsync(DocumentPaths.Document(documentId), cancellationToken);

        return entry is ObjectReadResult.Found found
            ? new StoredDocument(DocumentJson.Read(found.Content), found.ETag)
            : null;
    }

    /// <inheritdoc />
    public async Task<ObjectWriteResult> WriteAsync(
        Document document,
        WriteCondition condition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(condition);

        return await _objects.WriteAsync(
            DocumentPaths.Document(document.Id),
            DocumentJson.Write(document),
            condition,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ObjectWriteResult> WriteSectionTextAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        string text,
        WriteCondition condition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(sectionId);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(condition);

        return await _objects.WriteAsync(
            DocumentPaths.SectionText(documentId, sectionId),
            text,
            condition,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ObjectDeleteResult> DeleteSectionTextAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(sectionId);

        return await _objects.DeleteAsync(
            DocumentPaths.SectionText(documentId, sectionId),
            cancellationToken);
    }
}
