// Turns a document into the text of document.json and back. It is a file of its
// own because the shape of that file is a promise to docs/Datenmodell.md and not
// an implementation detail of the store: field names, their order, the spelling
// of the state and the form of the timestamps are decided here and nowhere else.

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

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
    /// <summary>
    /// How the file is written and read: names in camelCase as
    /// <c>docs/Konventionen.md</c>, section Sprache, requires, the state as the
    /// word the model uses, and every timestamp in UTC with a trailing
    /// <c>Z</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The encoder lets every letter through as itself, so
    /// <c>Musterstraße</c> stays readable instead of becoming an escape
    /// sequence, while the characters that matter in HTML keep being escaped -
    /// a title is shown on a page, and nothing about this file should depend on
    /// the page escaping it a second time.
    /// </para>
    /// <para>
    /// The line break is stated and not left to the platform. Without that, the
    /// same document would be written with a carriage return on a developer
    /// machine and without one in the Linux web app: every file would differ
    /// depending on where it was last saved, every comparison of two frozen
    /// states would be full of changes nobody made, and the version stamp of the
    /// local store, which is a hash of the content, would move for no reason.
    /// </para>
    /// </remarks>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters =
        {
            new JsonStringEnumConverter(),
            new UtcTimestampConverter(),
        },
    };

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

        return JsonSerializer.Serialize(file, Options);
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
        var file = JsonSerializer.Deserialize<DocumentFile>(content, Options)
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

    /// <summary>
    /// Writes a moment the way <c>docs/Datenmodell.md</c> shows it:
    /// <c>2026-09-22T08:14:00Z</c>.
    /// </summary>
    /// <remarks>
    /// Without it the serializer would write <c>+00:00</c> instead of the
    /// <c>Z</c>, which is the same moment but not the same file. The fraction of
    /// a second is written only when there is one, so a timestamp the
    /// application stamped - it counts in whole seconds - comes out exactly as
    /// the model shows it, while a value from somewhere else is still written
    /// without losing anything.
    /// </remarks>
    private sealed class UtcTimestampConverter : JsonConverter<DateTimeOffset>
    {
        private const string WholeSeconds = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        private const string WithFraction = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

        /// <inheritdoc />
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDateTimeOffset();

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            var utc = value.ToUniversalTime();
            var format = utc.UtcTicks % TimeSpan.TicksPerSecond == 0 ? WholeSeconds : WithFraction;

            writer.WriteStringValue(utc.ToString(format, CultureInfo.InvariantCulture));
        }
    }
}
