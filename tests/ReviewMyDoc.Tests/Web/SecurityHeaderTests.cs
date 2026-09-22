// Checks the security headers one by one. They are the ground floor of this
// application: set once in a middleware, they hold for every page anybody builds
// later, and nobody building a page will think of them again. That is exactly
// why each of them gets a test of its own here. A header that quietly falls away
// breaks nothing visible and would be noticed by nobody.

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Integration tests of <see cref="SecurityHeaders"/>.</summary>
public sealed class SecurityHeaderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>Takes the application the test class shares.</summary>
    /// <param name="factory">The application under test.</param>
    public SecurityHeaderTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // A file is what its Content-Type says it is. Without this header a browser
    // may sniff an uploaded document into something it runs.
    [Fact]
    public async Task Mime_sniffing_is_switched_off()
    {
        Assert.Equal("nosniff", await HeaderAsync("/", "X-Content-Type-Options"));
    }

    // A review link carries its token in the path. A full referrer would hand
    // that token to whatever site the reviewer clicks on next.
    [Fact]
    public async Task The_referrer_leaves_the_application_as_an_origin_only()
    {
        Assert.Equal("strict-origin-when-cross-origin", await HeaderAsync("/", "Referrer-Policy"));
    }

    // Says the same as frame-ancestors 'none', for the intermediaries that read
    // only the older header.
    [Fact]
    public async Task Framing_is_forbidden_by_the_older_header_as_well()
    {
        Assert.Equal("DENY", await HeaderAsync("/", "X-Frame-Options"));
    }

    // A foreign window that opens this application, or is opened by it, gets no
    // handle to its browsing context.
    [Fact]
    public async Task The_browsing_context_is_isolated()
    {
        Assert.Equal("same-origin", await HeaderAsync("/", "Cross-Origin-Opener-Policy"));
    }

    // Nothing of this application may be embedded as a resource by a foreign
    // site, the building blocks under /components/ included.
    [Fact]
    public async Task No_foreign_site_may_embed_a_resource_of_this_application()
    {
        Assert.Equal("same-origin", await HeaderAsync("/", "Cross-Origin-Resource-Policy"));
    }

    // The whole application is private. A review link in a search index would be
    // a leaked token.
    [Fact]
    public async Task Nothing_of_this_application_may_be_indexed()
    {
        Assert.Equal("noindex, nofollow", await HeaderAsync("/", "X-Robots-Tag"));
    }

    // The application needs no device capability, so it asks for none and can be
    // made to ask for none.
    [Fact]
    public async Task Every_device_capability_is_switched_off()
    {
        var value = await HeaderAsync("/", "Permissions-Policy");

        Assert.Equal(SecurityHeaders.PermissionsPolicy, value);
        Assert.Contains("camera=()", value, StringComparison.Ordinal);
        Assert.Contains("microphone=()", value, StringComparison.Ordinal);
        Assert.Contains("geolocation=()", value, StringComparison.Ordinal);
        Assert.Contains("payment=()", value, StringComparison.Ordinal);
    }

    // The policy itself, in full. It is a constant of the application, so the
    // test can compare against it and still be worth something: what it really
    // proves is that the header arrives at the client unchanged.
    [Fact]
    public async Task The_content_security_policy_arrives_in_full()
    {
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, await HeaderAsync("/", "Content-Security-Policy"));
    }

    // The directives the policy stands or falls with, each named on its own so a
    // loosened one cannot hide inside a changed constant.
    [Theory]
    [InlineData("default-src 'none'")]
    [InlineData("script-src 'self'")]
    [InlineData("style-src 'self'")]
    [InlineData("img-src 'self'")]
    [InlineData("font-src 'self'")]
    [InlineData("connect-src 'self'")]
    [InlineData("form-action 'self'")]
    [InlineData("base-uri 'none'")]
    [InlineData("frame-ancestors 'none'")]
    public async Task The_content_security_policy_names_the_directive(string directive)
    {
        Assert.Contains(directive, await HeaderAsync("/", "Content-Security-Policy"), StringComparison.Ordinal);
    }

    // The one word that would make most of the policy pointless. Whoever adds it
    // has to delete this test first, and then they have to mean it.
    [Fact]
    public async Task The_content_security_policy_allows_nothing_inline_and_no_foreign_origin()
    {
        var value = await HeaderAsync("/", "Content-Security-Policy");

        Assert.DoesNotContain("unsafe-inline", value, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", value, StringComparison.Ordinal);
        Assert.DoesNotContain("http:", value, StringComparison.Ordinal);
        Assert.DoesNotContain("https:", value, StringComparison.Ordinal);
        Assert.DoesNotContain("*", value, StringComparison.Ordinal);
    }

    // The other half of the assurance: a policy without 'unsafe-inline' is only
    // worth something if the pages really get by without inline styles and
    // scripts. A page that added one would load broken in a browser and green in
    // a test that only reads the header, so the markup is checked here as well.
    [Theory]
    [InlineData("/")]
    [InlineData("/Error")]
    public async Task The_rendered_page_carries_no_inline_style_and_no_inline_script(string path)
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync(path, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" style=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" onclick=", html, StringComparison.OrdinalIgnoreCase);
    }

    // The headers belong on every answer and not only on a rendered page. A
    // static file, a page and a path that does not exist are three different
    // ways out of the pipeline, and the middleware sits in front of all of them.
    [Theory]
    [InlineData("/")]
    [InlineData("/Error")]
    [InlineData("/css/site.css")]
    [InlineData("/components/tokens/tokens.css")]
    [InlineData("/gibt-es-nicht")]
    public async Task Every_kind_of_response_carries_the_headers(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, Single(response, "Content-Security-Policy"));
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.Equal("noindex, nofollow", Single(response, "X-Robots-Tag"));
    }

    // One value per header and not two. A header written twice is a header two
    // places decide, and the browser picks one of them.
    [Fact]
    public async Task No_header_is_sent_twice()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        foreach (var name in new[]
        {
            "Content-Security-Policy", "X-Content-Type-Options", "Referrer-Policy", "X-Frame-Options",
            "Cross-Origin-Opener-Policy", "Cross-Origin-Resource-Policy", "X-Robots-Tag",
            "Permissions-Policy",
        })
        {
            Assert.Single(response.Headers.GetValues(name));
        }
    }

    private async Task<string> HeaderAsync(string path, string name)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return Single(response, name);
    }

    private static string Single(HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"The header {name} is missing.");

        return Assert.Single(values!);
    }
}
