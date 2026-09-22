// Checks the frame every page of the application is rendered into: the
// stylesheets it loads and in which order, the language, the way to the
// content, the navigation and the footer. The frame is the one piece of markup
// no page writes itself, so a mistake in it is a mistake on every page at once.

using System.Net;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Integration tests of _Layout.cshtml.</summary>
public sealed class PageFrameTests : IClassFixture<OwnerApplication>
{
    private readonly OwnerApplication _factory;

    /// <summary>Takes the application the test class shares.</summary>
    /// <param name="factory">The application under test.</param>
    public PageFrameTests(OwnerApplication factory) => _factory = factory;

    // An empty page in the frame loads the tokens and the building blocks it
    // uses, all of them from /components/ and none of them from wwwroot.
    [Fact]
    public async Task An_empty_page_loads_the_tokens_and_the_building_blocks_of_the_frame()
    {
        var html = await GetHtmlAsync("/");

        Assert.Contains("href=\"/components/tokens/tokens.css\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/components/theme/theme.css\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/components/button/button.css\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/components/field/field.css\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/components/rows/rows.css\"", html, StringComparison.Ordinal);
        Assert.Contains("/css/site.css", html, StringComparison.Ordinal);
    }

    // The order is not decoration: every building block and site.css read the
    // values of the tokens, the theme writes the colour roles of the tokens over
    // and only wins at equal specificity because it stands after them, and
    // site.css sets the values of a building block.
    [Fact]
    public async Task The_tokens_stand_first_the_theme_after_them_and_site_css_last()
    {
        var html = await GetHtmlAsync("/");

        var tokens = html.IndexOf("/components/tokens/tokens.css", StringComparison.Ordinal);
        var theme = html.IndexOf("/components/theme/theme.css", StringComparison.Ordinal);
        var button = html.IndexOf("/components/button/button.css", StringComparison.Ordinal);
        var site = html.IndexOf("/css/site.css", StringComparison.Ordinal);

        Assert.True(tokens < theme, "The theme has to be loaded after the tokens.");
        Assert.True(theme < button, "The theme has to be loaded before the building blocks.");
        Assert.True(button < site, "site.css has to be loaded after the building blocks.");
    }

    // The frame belongs to every page, not only to the start page.
    [Theory]
    [InlineData("/")]
    [InlineData("/Error")]
    public async Task Every_page_carries_the_frame(string path)
    {
        var html = await GetHtmlAsync(path);

        Assert.Contains("<html lang=\"de\">", html, StringComparison.Ordinal);
        Assert.Contains("<body class=\"app\">", html, StringComparison.Ordinal);
        Assert.Contains("class=\"app__skip\" href=\"#inhalt\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"app__main\" id=\"inhalt\"", html, StringComparison.Ordinal);
        Assert.Contains("<footer class=\"app__footer\">", html, StringComparison.Ordinal);
    }

    // The navigation carries the terms of docs/Konzept.md, and the entry of the
    // page one stands on says so instead of only looking different.
    [Fact]
    public async Task The_navigation_names_the_overview_and_marks_the_current_page()
    {
        var html = await GetHtmlAsync("/");

        Assert.Contains("aria-label=\"Hauptnavigation\"", html, StringComparison.Ordinal);
        Assert.Contains("Übersicht", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", html, StringComparison.Ordinal);
    }

    // Away from the start page nothing in the navigation may claim to be the
    // current page.
    [Fact]
    public async Task Another_page_marks_no_entry_as_the_current_one()
    {
        var html = await GetHtmlAsync("/Error");

        Assert.DoesNotContain("aria-current", html, StringComparison.Ordinal);
    }

    private async Task<string> GetHtmlAsync(string path)
    {
        var client = await _factory.OwnerClientAsync();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);

        return await response.Content.ReadAsStringAsync();
    }
}
