// The one place that builds a path of the document aggregate. Every other file
// of this folder asks here, so a path of docs/Datenmodell.md appears as a string
// exactly once and a change to the storage layout is a change to one method.

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// Builds the paths under which a document and its section texts live in the
/// object store.
/// </summary>
/// <remarks>
/// <para>
/// <c>docs/Konventionen.md</c>, section Code, asks for exactly one place per
/// aggregate that builds paths, and this is it for the document. It is internal,
/// so no service and no page outside this folder can learn a path as a string;
/// they name a document and a section, and what that means in the store is
/// decided here.
/// </para>
/// <para>
/// The paths are the ones <c>docs/Datenmodell.md</c> lists, unchanged. Nothing
/// here validates: the two identifier types have already made sure the values
/// carry nothing but lower case letters, digits and the underscore, which is
/// what the object store demands of a path.
/// </para>
/// </remarks>
internal static class DocumentPaths
{
    /// <summary>The prefix under which everything of one document lives.</summary>
    /// <remarks>
    /// Internal and not private: <see cref="DocumentStore.ListDocumentsAsync"/>
    /// hands this exact prefix to <see cref="Storage.IObjectStore.ListAsync"/>,
    /// which is what turns the list of documents into an operation over the
    /// object store instead of a second, separately maintained index.
    /// </remarks>
    internal const string DocumentsPrefix = "documents/";

    /// <summary>The name of the metadata file of one document, without its folder.</summary>
    private const string DocumentFileName = "document.json";

    /// <summary>The path of the metadata and the outline of one document.</summary>
    /// <param name="documentId">Which document.</param>
    /// <returns><c>documents/{documentId}/document.json</c>.</returns>
    internal static string Document(DocumentIdentifier documentId) =>
        $"{DocumentsPrefix}{documentId.Value}/{DocumentFileName}";

    /// <summary>
    /// Tells a <c>document.json</c> apart from every other entry a document owns
    /// - a section text, a frozen version, a review order, a piece of feedback -
    /// once <see cref="Storage.IObjectStore.ListAsync"/> has answered with
    /// everything under <see cref="DocumentsPrefix"/>.
    /// </summary>
    /// <param name="path">One path <see cref="Storage.IObjectStore.ListAsync"/> returned.</param>
    /// <returns><see langword="true"/> for the one entry per document that names it.</returns>
    internal static bool IsDocumentEntry(string path) =>
        path.EndsWith($"/{DocumentFileName}", StringComparison.Ordinal);

    /// <summary>The path of the Markdown text of one section.</summary>
    /// <param name="documentId">Which document the section belongs to.</param>
    /// <param name="sectionId">Which section.</param>
    /// <returns><c>documents/{documentId}/sections/{sectionId}.md</c>.</returns>
    /// <remarks>
    /// The text keeps its own entry, and the extension stays <c>.md</c>, because
    /// <c>docs/Datenmodell.md</c> wants a section to be readable, savable and, if
    /// it comes to it, rescuable by hand without the application.
    /// </remarks>
    internal static string SectionText(DocumentIdentifier documentId, SectionIdentifier sectionId) =>
        $"{DocumentsPrefix}{documentId.Value}/sections/{sectionId.Value}.md";
}
