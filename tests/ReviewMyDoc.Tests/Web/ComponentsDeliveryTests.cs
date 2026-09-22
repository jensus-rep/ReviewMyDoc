// Checks the delivery of the repository folder components/ under /components/.
// Three assurances hang on it: the tokens really arrive at the browser, nothing
// above the folder can be read through it, and the repository parts of a
// building block stay in the repository. Each of the three is one line in
// Program.cs and would break without a sound.

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Integration tests of the delivery of the building blocks.</summary>
public sealed class ComponentsDeliveryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    /// <summary>Takes the application the test class shares.</summary>
    /// <param name="factory">The application under test.</param>
    public ComponentsDeliveryTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    // The one file every page depends on. Without it not a single value of the
    // design language reaches the browser.
    [Fact]
    public async Task Tokens_are_delivered_as_a_stylesheet()
    {
        var token = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/components/tokens/tokens.css", token);
        var content = await response.Content.ReadAsStringAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("--color-bg", content, StringComparison.Ordinal);
        Assert.Contains("prefers-color-scheme: dark", content, StringComparison.Ordinal);
    }

    // The building blocks the frame loads. A page that asks for them and gets a
    // 404 looks broken in a way no test of the markup would notice.
    [Theory]
    [InlineData("/components/button/button.css")]
    [InlineData("/components/field/field.css")]
    [InlineData("/components/rows/rows.css")]
    public async Task Every_building_block_of_the_frame_is_delivered(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
    }

    // The fonts of the website tokens lie in the folder as well, and a font that
    // is not delivered is a page in the wrong typeface.
    [Fact]
    public async Task A_font_of_the_building_blocks_is_delivered()
    {
        var path = "/components/tokens/fonts/p51-plan-400.woff2";

        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("font/woff2", response.Content.Headers.ContentType?.MediaType);
    }

    // The point of the own file provider: it opens the folder components/ and
    // nothing above it. The encoded forms are the ones that matter, because a
    // client normalises the plain one away before it is ever sent.
    [Theory]
    [InlineData("/components/../appsettings.json")]
    [InlineData("/components/%2e%2e/appsettings.json")]
    [InlineData("/components/tokens/%2e%2e/%2e%2e/appsettings.json")]
    [InlineData("/components/%2e%2e%2f%2e%2e%2fReviewMyDoc.sln")]
    public async Task A_path_with_two_dots_reaches_nothing_above_the_folder(string path)
    {
        var token = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(path, token);
        var content = await response.Content.ReadAsStringAsync(token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Storage", content, StringComparison.Ordinal);
    }

    // What belongs to the repository stays in the repository: the demo pages,
    // the READMEs and the licence of the fonts have no content type here, and
    // therefore no answer. See components/button/README.md, section Demo.
    [Theory]
    [InlineData("/components/button/demo.html")]
    [InlineData("/components/field/demo.html")]
    [InlineData("/components/README.md")]
    [InlineData("/components/rows/README.md")]
    [InlineData("/components/tokens/fonts/OFL.txt")]
    public async Task The_repository_parts_of_a_building_block_are_not_delivered(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // A directory is not a file. Without this the answer to a folder would be a
    // list of everything that lies in it.
    [Fact]
    public async Task The_folder_itself_is_not_a_listing()
    {
        var response = await _client.GetAsync("/components/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
