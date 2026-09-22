// Checks the named rate limiters and, above all, what a refusal looks like. The
// assurance of docs/Konventionen.md is not "a refused request gets 429"; it is
// that the visitor gets their page back, with their input and with a sentence,
// and that the server still says 429 with Retry-After. An empty 429 would pass a
// careless test and would be exactly the behaviour these limiters were built to
// avoid.

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReviewMyDoc.Web.Pages.Pruefung;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Integration tests of the named rate limiters and their refusal.</summary>
/// <remarks>
/// Every test builds an application of its own instead of sharing one. The
/// limiters are singletons and count across requests, which is the whole point
/// of them, so a shared application would let one test spend the budget of the
/// next and the order of the tests would decide whether they pass.
/// </remarks>
public sealed class RateLimitTests
{
    /// <summary>The address of the check page that asks the limiter of the sign-in.</summary>
    private const string ProbePath = "/pruefung/ratenbegrenzung";

    /// <summary>
    /// How many attempts the application under test allows, set low so that a
    /// test reaches the limit in a few requests instead of ten.
    /// </summary>
    private const int PermitLimit = 3;

    // Each limiter is registered under its own name, so a page that names one
    // cannot silently be handed another.
    [Theory]
    [InlineData(LoginRateLimit.Name)]
    [InlineData(ReviewLinkRateLimit.Name)]
    [InlineData(AiRateLimit.Name)]
    public void Every_limiter_is_registered_under_its_name(string name)
    {
        using var application = new Application();

        var limiter = application.Services.GetKeyedService<PartitionedRateLimiter<HttpContext>>(name);

        Assert.NotNull(limiter);
    }

    // The three names are the contract between this task and the pages that will
    // ask for them, and they are documented in docs/Betrieb.md under exactly
    // these names.
    [Fact]
    public void The_limiters_are_named_login_review_link_and_ai()
    {
        Assert.Equal("login", LoginRateLimit.Name);
        Assert.Equal("review-link", ReviewLinkRateLimit.Name);
        Assert.Equal("ai", AiRateLimit.Name);
    }

    // The whole point, in one test: the attempt after the limit is refused with
    // 429 and Retry-After, and the answer is the page, in its frame, with what
    // was typed.
    [Fact]
    public async Task A_refused_attempt_answers_429_with_retry_after_and_the_page_in_its_frame()
    {
        using var application = new Application();
        using var client = application.CreateClient();
        var token = await TokenAsync(client);

        for (var attempt = 1; attempt <= PermitLimit; attempt++)
        {
            var allowed = await PostAsync(client, token, $"Versuch {attempt}");

            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var refused = await PostAsync(client, token, "der Satz, den niemand verlieren will");
        var html = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The status and the header.
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.NotNull(refused.Headers.RetryAfter);

        // Not an empty response, but the page.
        Assert.Equal("text/html", refused.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(html);

        // The frame of the application, not a bare error body.
        Assert.Contains("<html lang=\"de\">", html, StringComparison.Ordinal);
        Assert.Contains("class=\"app__main\" id=\"inhalt\"", html, StringComparison.Ordinal);
        Assert.Contains("Hauptnavigation", html, StringComparison.Ordinal);
        Assert.Contains("/components/tokens/tokens.css", html, StringComparison.Ordinal);

        // The sentence and the input of the visitor.
        Assert.Contains(RatenbegrenzungModel.RefusedMessage, html, StringComparison.Ordinal);
        Assert.Contains("der Satz, den niemand verlieren will", html, StringComparison.Ordinal);
    }

    // Retry-After in whole seconds and at least one. A zero would invite the
    // next attempt immediately and make the header worthless.
    [Fact]
    public async Task Retry_after_names_at_least_one_whole_second()
    {
        using var application = new Application();
        using var client = application.CreateClient();

        var refused = await RefusedAsync(client);
        var seconds = Assert.Single(refused.Headers.GetValues("Retry-After"));

        Assert.True(
            int.TryParse(seconds, NumberStyles.None, CultureInfo.InvariantCulture, out var value),
            $"Retry-After '{seconds}' is not a whole number of seconds.");
        Assert.True(value >= 1, "Retry-After has to be at least one second.");
    }

    // Reading a page costs nothing and must not use up the budget of the person
    // who wants to act. Many reads, and the first attempt still goes through.
    [Fact]
    public async Task Reading_the_page_does_not_spend_the_budget()
    {
        using var application = new Application();
        using var client = application.CreateClient();

        for (var read = 0; read < PermitLimit * 3; read++)
        {
            var page = await client.GetAsync(ProbePath, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        }

        var token = await TokenAsync(client);
        var response = await PostAsync(client, token, "nach vielen Aufrufen");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A refused response is still a response of this application, so it carries
    // the security headers like every other one.
    [Fact]
    public async Task A_refused_response_carries_the_security_headers()
    {
        using var application = new Application();
        using var client = application.CreateClient();

        var refused = await RefusedAsync(client);

        Assert.Equal(
            SecurityHeaders.ContentSecurityPolicy,
            Assert.Single(refused.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(refused.Headers.GetValues("X-Content-Type-Options")));
    }

    /// <summary>Spends the whole budget and returns the refused response.</summary>
    private static async Task<HttpResponseMessage> RefusedAsync(HttpClient client)
    {
        var token = await TokenAsync(client);
        HttpResponseMessage? response = null;

        for (var attempt = 1; attempt <= PermitLimit + 1; attempt++)
        {
            response = await PostAsync(client, token, "x");
        }

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);

        return response;
    }

    /// <summary>Reads the antiforgery token out of the form of the check page.</summary>
    /// <remarks>
    /// Fetched the way a browser does. That matters beyond convenience: a
    /// request antiforgery has already rejected never reaches the handler, so
    /// this also shows that the limiter is asked inside a request that got that
    /// far, and not instead of the check.
    /// </remarks>
    private static async Task<string> TokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync(ProbePath, TestContext.Current.CancellationToken);
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(match.Success, "The form of the check page carries no antiforgery token.");

        return match.Groups[1].Value;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string note)
    {
        var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>(nameof(RatenbegrenzungModel.Note), note),
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        ]);

        return client.PostAsync(ProbePath, form, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The application under test: in development, so the check page has a
    /// route, and with a limit small enough to reach in a handful of requests.
    /// </summary>
    public sealed class Application : WebApplicationFactory<Program>
    {
        /// <inheritdoc />
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment(Environments.Development);

            // UseSetting and not ConfigureAppConfiguration: the latter reaches
            // the configuration only after Program.cs has run, which is too late
            // for anything a registration reads while the application starts.
            builder.UseSetting(
                "RateLimits:Login:PermitLimit",
                PermitLimit.ToString(CultureInfo.InvariantCulture));
        }
    }
}
