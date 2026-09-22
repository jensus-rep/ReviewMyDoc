// Holds MarkdigMarkdownRenderer to the done criteria of tsk_gSNGyQOTFj: the
// elements docs/Konzept.md allows come out as HTML, everything that could turn
// a section into an attack on its reader does not, every surviving link is
// marked noopener noreferrer, and the same text always renders to the same
// bytes.

using ReviewMyDoc.Infrastructure.Markdown;

namespace ReviewMyDoc.Tests.Markdown;

/// <summary>Renders Markdown with a fresh renderer per test, the way a request would.</summary>
public sealed class MarkdigMarkdownRendererTests
{
    private readonly MarkdigMarkdownRenderer _renderer = new();

    [Fact]
    public void A_heading_is_rendered()
    {
        var html = _renderer.Render("# Ausgangslage");

        Assert.Contains("<h1>Ausgangslage</h1>", html);
    }

    [Fact]
    public void Bold_and_italic_are_rendered()
    {
        var html = _renderer.Render("**wichtig** und *betont*");

        Assert.Contains("<strong>wichtig</strong>", html);
        Assert.Contains("<em>betont</em>", html);
    }

    [Fact]
    public void A_list_is_rendered()
    {
        var html = _renderer.Render("- eins\n- zwei");

        Assert.Contains("<ul>", html);
        Assert.Contains("<li>eins</li>", html);
        Assert.Contains("<li>zwei</li>", html);
    }

    [Fact]
    public void A_table_is_rendered()
    {
        var html = _renderer.Render("| A | B |\n| --- | --- |\n| 1 | 2 |");

        Assert.Contains("<table>", html);
        Assert.Contains("<td>1</td>", html);
    }

    [Fact]
    public void A_quote_is_rendered()
    {
        var html = _renderer.Render("> so stand es im Vertrag");

        Assert.Contains("<blockquote>", html);
    }

    [Fact]
    public void Code_is_rendered()
    {
        var html = _renderer.Render("Ein `inline`-Ausdruck und:\n\n```\nfenced\n```");

        Assert.Contains("<code>inline</code>", html);
        Assert.Contains("<pre><code>", html);
    }

    [Fact]
    public void A_link_is_rendered_and_marked_noopener_noreferrer()
    {
        var html = _renderer.Render("[Vertrag](https://example.org/vertrag)");

        Assert.Contains("href=\"https://example.org/vertrag\"", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
    }

    // The attempt: embed a script tag directly in a section's Markdown, the way
    // a reviewer's pasted suggestion could. DisableHtml() turns raw markup into
    // literal text, so the tag is only ever visible as escaped characters, never
    // as something a browser executes.
    [Fact]
    public void An_embedded_script_does_not_survive()
    {
        var html = _renderer.Render("Vorher\n\n<script>alert('x')</script>\n\nNachher");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    // Same attempt, with a style tag - the other element the done criteria name
    // by itself, because it can rewrite how the surrounding page looks rather
    // than just this section's content.
    [Fact]
    public void An_embedded_style_tag_does_not_survive()
    {
        var html = _renderer.Render("<style>body { display: none; }</style>");

        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
    }

    // The attempt: an ordinary looking element that carries an event attribute.
    // A tag search for "<img" or "<div" catches it whether or not the escaped
    // word "onerror" still happens to appear as visible text afterwards - what
    // matters is that it is never a live attribute of a live tag.
    [Fact]
    public void An_embedded_element_with_an_event_attribute_does_not_survive()
    {
        var html = _renderer.Render("<img src=\"x\" onerror=\"alert('x')\">");

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
    }

    // The attempt: a link whose address is not an address at all but a script.
    // Markdown's own link syntax is not raw HTML, so DisableHtml() alone would
    // let this one through; the address is checked separately.
    [Fact]
    public void A_javascript_link_does_not_survive()
    {
        var html = _renderer.Render("[klick mich](javascript:alert('x'))");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);

        // Not merely an address that leads nowhere: no anchor at all, and the
        // text the author wrote still there.
        Assert.DoesNotContain("<a", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("klick mich", html, StringComparison.Ordinal);
    }

    // The same attempt written as an autolink, Markdown's other link syntax.
    [Fact]
    public void A_javascript_autolink_does_not_survive()
    {
        var html = _renderer.Render("<javascript:alert('x')>");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    // The attempt: a data: address, the usual way an image smuggles a payload
    // in rather than merely pointing at one. Images are not one of the elements
    // docs/Konzept.md allows in the first place, so the whole image disappears,
    // address included.
    [Fact]
    public void A_data_link_does_not_survive()
    {
        var html = _renderer.Render("![Diagramm](data:image/png;base64,AAAA)");

        Assert.DoesNotContain("data:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
    }

    // A data: address as a link and not as an image. The image above never gets
    // as far as its address; this one does, and has to be stopped by the scheme
    // check itself. Both ways are named in the done criteria of the task.
    [Fact]
    public void A_data_link_that_is_not_an_image_does_not_survive()
    {
        var html = _renderer.Render("[Bericht](data:text/html;base64,PHNjcmlwdD4=)");

        Assert.DoesNotContain("data:", html, StringComparison.OrdinalIgnoreCase);
    }

    // The scheme written in mixed case. A browser reads a scheme without regard
    // to case, so a check that does not would be no check at all.
    [Fact]
    public void A_javascript_link_in_mixed_case_does_not_survive()
    {
        var html = _renderer.Render("[klick mich](JaVaScRiPt:alert('x'))");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a", html, StringComparison.OrdinalIgnoreCase);
    }

    // The scheme hidden behind HTML entities. Markdown resolves them while it
    // reads the address, so what the cleanup gets to see is the plain text
    // again - but only if the cleanup runs after that step and not before.
    [Fact]
    public void A_javascript_link_written_with_entities_does_not_survive()
    {
        var html = _renderer.Render("[klick mich](&#106;avascript&#58;alert&#40;1&#41;)");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", html, StringComparison.OrdinalIgnoreCase);
    }

    // A control character hidden inside the scheme is a known way to slip a
    // dangerous address past a naive check that only looks for the literal text
    // "javascript:"; a browser skips control characters when it reads a scheme,
    // so the address still runs even though the string does not match.
    [Fact]
    public void A_javascript_link_with_an_embedded_control_character_does_not_survive()
    {
        var html = _renderer.Render("[klick mich](java\tscript:alert('x'))");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a", html, StringComparison.OrdinalIgnoreCase);
    }

    // The text of a blocked link is not only kept, it keeps its shape: what was
    // emphasised inside the link text is still emphasised without it.
    [Fact]
    public void The_text_of_a_blocked_link_keeps_its_emphasis()
    {
        var html = _renderer.Render("[ein **wichtiger** Hinweis](javascript:alert('x'))");

        Assert.Contains("ein <strong>wichtiger</strong> Hinweis", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<a", html, StringComparison.OrdinalIgnoreCase);
    }

    // A relative address carries no scheme to abuse and is not the target of
    // this cleanup; it must keep working, or every cross-reference between
    // sections of the same document would break along with the dangerous links.
    [Fact]
    public void A_relative_link_survives_and_is_still_marked()
    {
        var html = _renderer.Render("[siehe oben](#ausgangslage)");

        Assert.Contains("href=\"#ausgangslage\"", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
    }

    // The same Markdown text rendered twice must be byte for byte the same
    // output; nothing behind IMarkdownRenderer may depend on the clock, on a
    // random source or on the order a set happens to enumerate in.
    [Fact]
    public void Rendering_the_same_text_twice_gives_byte_for_byte_the_same_html()
    {
        const string markdown = "# Titel\n\nEin **Absatz** mit *Betonung*, einer [Quelle](https://example.org) "
            + "und einer Liste:\n\n- eins\n- zwei\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\n\n> ein Zitat\n\n"
            + "`code` und\n\n```\nfenced\n```\n";

        var first = _renderer.Render(markdown);
        var second = _renderer.Render(markdown);
        var third = new MarkdigMarkdownRenderer().Render(markdown);

        Assert.Equal(first, second, StringComparer.Ordinal);
        Assert.Equal(first, third, StringComparer.Ordinal);
    }

    [Fact]
    public void A_null_argument_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => _renderer.Render(null!));
    }
}
