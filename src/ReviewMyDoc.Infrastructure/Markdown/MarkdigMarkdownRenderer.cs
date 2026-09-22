// The implementation of IMarkdownRenderer: Markdig does the parsing and the
// HTML rendering, and the syntax tree it builds is cleaned in between, so that
// what Markdig would otherwise render faithfully - an embedded <script>, an
// on-attribute, a javascript: or data: link - never reaches the page.

using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ReviewMyDoc.Infrastructure.Markdown;

/// <summary>
/// Renders the Markdown of a section to HTML with Markdig, restricted to the
/// elements named in <c>docs/Konzept.md</c> and cleaned of anything else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two layers, not one.</b> The pipeline is built with
/// <see cref="MarkdownPipelineBuilderExtensions.DisableHtml"/>, so raw markup
/// in the Markdown text - a pasted <c>&lt;script&gt;</c>, a <c>&lt;style&gt;</c>
/// block, an element carrying an <c>on…</c> attribute - is parsed as literal
/// text and therefore HTML-escaped on the way out; it is never handed to the
/// browser as a tag. That layer alone does not reach a link: Markdown's own
/// link syntax, <c>[text](url)</c>, is not raw HTML and would render a
/// <c>javascript:</c> or <c>data:</c> address exactly as written. The second
/// layer, <see cref="Sanitize"/>, walks the parsed tree before it is rendered
/// and removes what the elements list of <c>docs/Konzept.md</c> does not name
/// - images above all - and checks every remaining link's address against an
/// allow list of schemes.
/// </para>
/// <para>
/// <b>Why the tree and not the finished HTML string.</b> Parsing HTML back out
/// of Markdig's own output to clean it would need a second library, exactly
/// what the task asks to avoid if Markdig's own means suffice. Markdig already
/// hands out the parsed tree before rendering it, so the cleanup happens where
/// the structure is still explicit - a link is a <see cref="LinkInline"/> with
/// a <c>Url</c> property, not a string to search - and no second package is
/// needed.
/// </para>
/// <para>
/// <b>Stateless and safe to share.</b> The pipeline is built once and reused;
/// Markdig's pipeline and parser are immutable once built and carry no state
/// between calls, which is also what makes the renderer deterministic: the
/// same Markdown text walks the same parsers in the same order every time.
/// </para>
/// </remarks>
public sealed class MarkdigMarkdownRenderer : ReviewMyDoc.Core.Markdown.IMarkdownRenderer
{
    /// <summary>
    /// The schemes a link or an autolink may use. Anything else - most of all
    /// <c>javascript:</c> and <c>data:</c> - is stripped rather than rendered.
    /// A link without a scheme, such as <c>#section</c> or a relative path,
    /// carries no protocol to abuse and is always allowed.
    /// </summary>
    private static readonly string[] AllowedSchemes =
    [
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        Uri.UriSchemeMailto,
    ];

    /// <summary>
    /// The value every surviving link is given for its <c>rel</c> attribute:
    /// <c>noopener</c> so the link cannot reach back into this window through
    /// <c>window.opener</c>, <c>noreferrer</c> so it does not announce where it
    /// was clicked from.
    /// </summary>
    private const string LinkRelation = "noopener noreferrer";

    private readonly MarkdownPipeline _pipeline;

    /// <summary>
    /// Builds the renderer with the one pipeline it uses for every call:
    /// Markdig's core elements - heading, emphasis, list, quote, code - plus
    /// pipe tables for docs/Konzept.md's table, raw HTML turned off.
    /// </summary>
    public MarkdigMarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .DisableHtml()
            .Build();
    }

    /// <inheritdoc />
    public string Render(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var document = Markdig.Markdown.Parse(markdown, _pipeline);
        Sanitize(document);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        _pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();

        return writer.ToString();
    }

    /// <summary>
    /// Walks every block of the parsed document and, through
    /// <see cref="SanitizeInlines"/>, every inline in it.
    /// </summary>
    private static void Sanitize(MarkdownDocument document)
    {
        foreach (var block in document)
        {
            SanitizeBlock(block);
        }
    }

    /// <summary>Descends into a block's children or its inline content, whichever it has.</summary>
    private static void SanitizeBlock(Block block)
    {
        if (block is ContainerBlock container)
        {
            // A snapshot: a table or a list can be restructured by nothing this
            // method does, but taking the same defensive copy the inline walk
            // needs keeps the two symmetrical and safe to change together.
            foreach (var child in container.ToList())
            {
                SanitizeBlock(child);
            }

            return;
        }

        if (block is LeafBlock { Inline: { } inline })
        {
            SanitizeInlines(inline);
        }
    }

    /// <summary>
    /// Removes every image and checks every link and autolink of one inline
    /// container, then descends into whatever container inlines are left - the
    /// text a link wraps, the content of an emphasis, and so on.
    /// </summary>
    /// <remarks>
    /// Iterates over a materialized copy of the children: an image is detached
    /// from the tree while this loop runs, and mutating the linked list Markdig
    /// walks live would either skip a sibling or visit one twice.
    /// </remarks>
    private static void SanitizeInlines(ContainerInline container)
    {
        foreach (var child in container.ToList())
        {
            switch (child)
            {
                case LinkInline { IsImage: true } image:
                    // Not one of the elements docs/Konzept.md names. Removing it
                    // outright, alt text included, also takes care of the
                    // classic data: URI image on its own: there is no src left
                    // for it to hide in.
                    image.Remove();
                    break;

                case LinkInline link:
                    SanitizeLink(link);
                    SanitizeInlines(link);
                    break;

                case AutolinkInline autolink:
                    SanitizeAutolink(autolink);
                    break;

                case ContainerInline nested:
                    SanitizeInlines(nested);
                    break;
            }
        }
    }

    /// <summary>
    /// Turns a link whose scheme is not on the allow list back into the text it
    /// wraps, and marks every surviving link with
    /// <c>rel="noopener noreferrer"</c>.
    /// </summary>
    private static void SanitizeLink(LinkInline link)
    {
        if (!IsAllowedUrl(link.Url))
        {
            Unlink(link);

            return;
        }

        link.GetAttributes().AddPropertyIfNotExist("rel", LinkRelation);
    }

    /// <summary>
    /// Replaces a link with its own content: the text the author wrote stays
    /// visible, the anchor around it is gone.
    /// </summary>
    /// <remarks>
    /// Merely clearing <c>Url</c> would leave <c>&lt;a href=""&gt;</c> behind,
    /// which still looks like a link and reloads the page when it is clicked.
    /// Nothing dangerous, but nothing a reader can make sense of either. The
    /// children move one after another so their order survives, and emphasis or
    /// code inside the link text survives with them.
    /// </remarks>
    private static void Unlink(LinkInline link)
    {
        Inline previous = link;

        foreach (var child in link.ToList())
        {
            child.Remove();
            previous.InsertAfter(child);
            previous = child;
        }

        link.Remove();
    }

    /// <summary>
    /// Removes an autolink - the <c>&lt;https://example.org&gt;</c> syntax,
    /// whose visible text and address are the same string - outright if its
    /// scheme is not on the allow list, since blanking the address would also
    /// blank the only text it has. A safe one is marked like any other link.
    /// </summary>
    private static void SanitizeAutolink(AutolinkInline autolink)
    {
        if (autolink.IsEmail)
        {
            // mailto: implied, always on the allow list.
            autolink.GetAttributes().AddPropertyIfNotExist("rel", LinkRelation);

            return;
        }

        if (!IsAllowedUrl(autolink.Url))
        {
            autolink.Remove();

            return;
        }

        autolink.GetAttributes().AddPropertyIfNotExist("rel", LinkRelation);
    }

    /// <summary>Answers whether a link address is safe to render.</summary>
    /// <remarks>
    /// A browser ignores an ASCII control character while it reads the scheme
    /// of a URL, so <c>"java\tscript:alert(1)"</c> still runs as script even
    /// though a literal search for <c>"javascript:"</c> would miss it. Control
    /// characters are stripped before the scheme is read, closing that gap, the
    /// same way a real HTML sanitizer would.
    /// </remarks>
    private static bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        var normalized = new string(url.Where(character => !char.IsControl(character)).ToArray()).TrimStart();

        var colon = normalized.IndexOf(':');
        if (colon < 0)
        {
            // No scheme: a relative address such as "#section" or "kap-2.md"
            // carries no protocol to abuse.
            return true;
        }

        var scheme = normalized[..colon];

        return AllowedSchemes.Any(allowed => scheme.Equals(allowed, StringComparison.OrdinalIgnoreCase));
    }
}
