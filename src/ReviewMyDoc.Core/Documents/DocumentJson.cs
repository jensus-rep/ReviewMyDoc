// Turns a document into the text of document.json and back. It is a file of its
// own because the shape of that file is a promise to docs/Datenmodell.md and not
// an implementation detail of the store: field names, their order, the spelling
// of the state and the form of the timestamps are decided here and nowhere else.

using System.Text.Json;

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// Reads and writes the <c>document.json</c> of <c>docs/Datenmodell.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// The file is written by this application alone. So anything that cannot be
/// turned into a <see cref="Document"/> is a defect - a file from another
/// version, a truncated write, an edit by hand that went wrong - and not a case
/// the user interface has a message for. It is reported as a
/// <see cref="JsonException"/> and reaches the error page, unlike a missing
/// entry or a failed condition, which are result values.
/// </para>
/// <para>
/// Written with indentation and with umlauts as themselves. The file is small,
/// it is read by people when something has to be understood or rescued, and it
/// sits in a version history where a change of one field should not rewrite one
/// long line.
/// </para>
/// </remarks>
internal static class DocumentJson
{
    /// <summary>Writes a document as the text of its <c>document.json</c>.</summary>
    /// <param name="document">The document to write.</param>
    /// <returns>The complete content of the file.</returns>
    internal static string Write(Document document)
    {
        var file = new DocumentFile
        {
            Id = document.Id.Value,
            OwnerId = document.OwnerId,
            Title = document.Title,
            State = document.State,
            Version = document.Version,
            Sections = [.. document.Sections.Select(section => new SectionFile
            {
                Id = section.Id.Value,
                Heading = section.Heading,
                Order = section.Order,
                UpdatedAt = section.UpdatedAt,
            })],
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
        };

        return JsonSerializer.Serialize(file, DocumentJsonOptions.Options);
    }

    /// <summary>Reads the text of a <c>document.json</c> back into a document.</summary>
    /// <param name="content">The content of the file.</param>
    /// <returns>The document the file describes.</returns>
    /// <exception cref="JsonException">
    /// The content is not a <c>document.json</c> of this model: it is not JSON,
    /// a field is missing, a value does not fit or the identifiers break the
    /// rules of <c>docs/Datenmodell.md</c>.
    /// </exception>
    internal static Document Read(string content)
    {
        var file = JsonSerializer.Deserialize<DocumentFile>(content, DocumentJsonOptions.Options)
            ?? throw new JsonException("A document.json holds an object and not the literal null.");

        try
        {
            var sections = file.Sections
                .Select(section => new Section(
                    new SectionIdentifier(section.Id),
                    section.Heading,
                    section.Order,
                    section.UpdatedAt))
                .ToArray();

            return new Document(
                new DocumentIdentifier(file.Id),
                file.OwnerId,
                file.Title,
                file.State,
                file.Version,
                sections,
                file.CreatedAt,
                file.UpdatedAt);
        }
        catch (ArgumentException exception)
        {
            // The rules of the model - a valid identifier, a title that is not
            // empty, no section twice - are kept by the types above, which state
            // them as argument failures. Read from a file they mean the file is
            // broken, so they are reported the same way as JSON that does not
            // parse, and the original failure stays attached for the log.
            throw new JsonException("The content is not a document.json of this model.", exception);
        }
    }

    /// <summary>
    /// The fields of <c>document.json</c> in the order
    /// <c>docs/Datenmodell.md</c> lists them.
    /// </summary>
    /// <remarks>
    /// A type of its own instead of serializing <see cref="Document"/> directly,
    /// so the file keeps its shape when the domain type grows a member, and so
    /// the order of the fields is a decision that can be read off one place.
    /// Every property is <c>required</c>: a file that lacks one is refused
    /// instead of quietly becoming a document with a default value in it.
    /// </remarks>
    private sealed record DocumentFile
    {
        public required string Id { get; init; }

        public required string OwnerId { get; init; }

        public required string Title { get; init; }

        public required DocumentState State { get; init; }

        public required int Version { get; init; }

        public required IReadOnlyList<SectionFile> Sections { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }
    }

    /// <summary>One entry of the list <c>sections</c>.</summary>
    private sealed record SectionFile
    {
        public required string Id { get; init; }

        public required string Heading { get; init; }

        public required int Order { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
