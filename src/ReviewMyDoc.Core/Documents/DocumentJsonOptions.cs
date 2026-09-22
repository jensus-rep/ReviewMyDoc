// The one JSON shape every file of this aggregate is written and read with:
// camelCase names, indentation, real umlauts and UTC timestamps with a trailing
// Z. Pulled out of DocumentJson so that DocumentVersionJson, which needs the
// same rules for a differently shaped file, cannot drift into a second spelling
// of them.

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// The <see cref="JsonSerializerOptions"/> shared by every file of the document
/// aggregate: <c>document.json</c> and <c>versions/{version}.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// The encoder lets every letter through as itself, so <c>Musterstraße</c>
/// stays readable instead of becoming an escape sequence, while the characters
/// that matter in HTML keep being escaped - a title or a section text is shown
/// on a page, and nothing about these files should depend on the page escaping
/// it a second time.
/// </para>
/// <para>
/// The line break is stated and not left to the platform. Without that, the
/// same file would be written with a carriage return on a developer machine and
/// without one in the Linux web app: every file would differ depending on where
/// it was last saved, and a frozen version, once written, would look different
/// merely for having been produced somewhere else - and it is never written
/// again to fix that.
/// </para>
/// </remarks>
internal static class DocumentJsonOptions
{
    /// <summary>The options both files of this aggregate are serialized with.</summary>
    internal static readonly JsonSerializerOptions Options = new()
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
