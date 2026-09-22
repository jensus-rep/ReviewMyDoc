// The one door from a section's Markdown text to the HTML a browser is shown.
// Core knows only that this translation exists; Markdig and the cleanup that
// keeps the result safe live in ReviewMyDoc.Infrastructure, so the domain never
// carries a package reference for either. See docs/Konventionen.md, section
// Code, and the Projektgrenzen-Tests in tests/ReviewMyDoc.Tests.

namespace ReviewMyDoc.Core.Markdown;

/// <summary>
/// Turns the Markdown text of a section into HTML that is safe to render, no
/// matter where the text came from - the owner's own editor or a suggested
/// change a reviewer sent back over the network.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only the elements docs/Konzept.md allows.</b> Heading, bold, italic,
/// list, table, quote, link and code are rendered; embedded HTML, a
/// <c>script</c> or <c>style</c> tag, an <c>on…</c> attribute, and a
/// <c>javascript:</c> or <c>data:</c> link do not survive the translation,
/// whether they came from the Markdown text itself or were smuggled in as raw
/// markup.
/// </para>
/// <para>
/// <b>Every surviving link</b> carries <c>rel="noopener noreferrer"</c>, so a
/// link a reviewer opens can neither reach back into this application's
/// window nor announce where it was clicked from.
/// </para>
/// <para>
/// <b>Deterministic.</b> The same Markdown text renders to the same HTML,
/// byte for byte, every time. Nothing behind this interface reads the clock or
/// a random source.
/// </para>
/// </remarks>
public interface IMarkdownRenderer
{
    /// <summary>Renders one section's Markdown text to safe HTML.</summary>
    /// <param name="markdown">The section text as Markdown.</param>
    /// <returns>
    /// HTML built only from the allowed elements, cleaned of embedded markup,
    /// scripts, event attributes and dangerous link targets.
    /// </returns>
    string Render(string markdown);
}
