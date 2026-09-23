// Turns a frozen state into the text of versions/{version}.json and back. A file
// of its own and not a branch of DocumentJson, because the shape differs in the
// one way that matters: this file carries the text of every section, which
// document.json deliberately never does. It shares DocumentJsonOptions so the
// two files still agree on names, indentation and timestamps.

using System.Text.Json;

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// Reads and writes the <c>versions/{version}.json</c> of
/// <c>docs/Datenmodell.md</c>.
/// </summary>
/// <remarks>
/// As with <see cref="DocumentJson"/>, the file is written by this application
/// alone, so anything that will not parse into a <see cref="DocumentVersion"/>
/// is a defect and is reported as a <see cref="JsonException"/> rather than a
/// result value - it never happens through ordinary use, because a version, once
/// written, is never written again.
/// </remarks>
internal static class DocumentVersionJson
{
    /// <summary>Writes a frozen state as the text of its <c>versions/{version}.json</c>.</summary>
    /// <param name="version">The frozen state to write.</param>
    /// <returns>The complete content of the file.</returns>
    internal static string Write(DocumentVersion version)
    {
        var file = new DocumentVersionFile
        {
            DocumentId = version.DocumentId.Value,
            Version = version.Version,
            Title = version.Title,
            Sections = [.. version.Sections.Select(section => new FrozenSectionFile
            {
                Id = section.Id.Value,
                Heading = section.Heading,
                Order = section.Order,
                Text = section.Text,
            })],
            FrozenAt = version.FrozenAt,
        };

        return JsonSerializer.Serialize(file, DocumentJsonOptions.Options);
    }

    /// <summary>Reads the text of a <c>versions/{version}.json</c> back into a frozen state.</summary>
    /// <param name="content">The content of the file.</param>
    /// <returns>The frozen state the file describes.</returns>
    /// <exception cref="JsonException">
    /// The content is not a <c>versions/{version}.json</c> of this model: it is
    /// not JSON, a field is missing, a value does not fit or the identifiers
    /// break the rules of <c>docs/Datenmodell.md</c>.
    /// </exception>
    internal static DocumentVersion Read(string content)
    {
        var file = JsonSerializer.Deserialize<DocumentVersionFile>(content, DocumentJsonOptions.Options)
            ?? throw new JsonException("A versions/{version}.json holds an object and not the literal null.");

        try
        {
            var sections = file.Sections
                .Select(section => new FrozenSection(
                    new SectionIdentifier(section.Id),
                    section.Heading,
                    section.Order,
                    section.Text))
                .ToArray();

            return new DocumentVersion(
                new DocumentIdentifier(file.DocumentId),
                file.Version,
                file.Title,
                sections,
                file.FrozenAt);
        }
        catch (ArgumentException exception)
        {
            // The same reasoning as DocumentJson.Read: the rules of the model are
            // kept by the types above and state their violation as an argument
            // failure, which read back from a file means the file is broken.
            throw new JsonException("The content is not a versions/{version}.json of this model.", exception);
        }
    }

    /// <summary>
    /// The fields of <c>versions/{version}.json</c>, in the order this store
    /// writes them.
    /// </summary>
    /// <remarks>
    /// A type of its own instead of serializing <see cref="DocumentVersion"/>
    /// directly, for the same reason <c>DocumentFile</c> exists beside
    /// <see cref="Document"/>: the file keeps its shape when the domain type
    /// grows a member. Every property is <c>required</c>, so a truncated or
    /// hand-edited file is refused instead of quietly losing a section's text.
    /// </remarks>
    private sealed record DocumentVersionFile
    {
        public required string DocumentId { get; init; }

        public required int Version { get; init; }

        public required string Title { get; init; }

        public required IReadOnlyList<FrozenSectionFile> Sections { get; init; }

        public required DateTimeOffset FrozenAt { get; init; }
    }

    /// <summary>One entry of the list <c>sections</c>, with its text.</summary>
    private sealed record FrozenSectionFile
    {
        public required string Id { get; init; }

        public required string Heading { get; init; }

        public required int Order { get; init; }

        public required string Text { get; init; }
    }
}
