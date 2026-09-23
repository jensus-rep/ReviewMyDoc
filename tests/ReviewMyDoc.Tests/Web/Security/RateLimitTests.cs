// Checks the named rate limiters and what a refusal looks like. The assurance
// of docs/Konventionen.md is not "a refused request gets 429"; it is that the
// visitor gets their page back, with a sentence, and that the server still says
// 429 with Retry-After. An empty 429 would pass a careless test and would be
// exactly the behaviour these limiters were built to avoid.
//
// The page that asks the limiter here is the sign-in. Until it existed, a check
// page under Pages/Pruefung/ stood in for it; it was removed with the sign-in,
// because a stand-in that is no longer needed is only surface. That the whole
// shape of a refusal is right is shown by AnmeldungTests, on the real limit of
// the application; what is left here is what belongs to the limiters themselves.

using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
    /// <summary>The address of the page that asks the limiter of the sign-in.</summary>
    private const string LoginPath = OwnerAuthentication.LoginPath;

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
        using var application = new OwnerApplication();

        var limiter = application.Services.GetKeyedService<PartitionedRateLimiter<HttpContext>>(name);

        Assert.NotNull(limiter);
    }

    // The three names are the contract between the limiters and the pages that
    // ask for them, and they are documented in docs/Betrieb.md under exactly
    // these names.
    [Fact]
    public void The_limiters_are_named_login_review_link_and_ai()
    {
        Assert.Equal("login", LoginRateLimit.Name);
        Assert.Equal("review-link", ReviewLinkRateLimit.Name);
        Assert.Equal("ai", AiRateLimit.Name);
    }

    // Retry-After in whole seconds and at least one. A zero would invite the
    // next attempt immediately and make the header worthless.
    [Fact]
    public async Task Retry_after_names_at_least_one_whole_second()
    {
        using var application = new OwnerApplication { LoginPermitLimit = PermitLimit };
        using var client = application.CreateAnonymousClient();

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
        using var application = new OwnerApplication { LoginPermitLimit = PermitLimit };
        using var client = application.CreateAnonymousClient();

        for (var read = 0; read < PermitLimit * 3; read++)
        {
            var page = await client.GetAsync(LoginPath, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        }

        var token = await OwnerApplication.AntiforgeryTokenAsync(client, LoginPath);
        var response = await OwnerApplication.SignInAsync(client, token, OwnerApplication.WrongPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A refused response is still a response of this application, so it carries
    // the security headers like every other one.
    [Fact]
    public async Task A_refused_response_carries_the_security_headers()
    {
        using var application = new OwnerApplication { LoginPermitLimit = PermitLimit };
        using var client = application.CreateAnonymousClient();

        var refused = await RefusedAsync(client);

        Assert.Equal(
            SecurityHeaders.ContentSecurityPolicy,
            Assert.Single(refused.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(refused.Headers.GetValues("X-Content-Type-Options")));
    }

    /// <summary>Spends the whole budget and returns the refused response.</summary>
    /// <remarks>
    /// The token is read the way a browser reads it, out of the form. That
    /// matters beyond convenience: a request antiforgery has already rejected
    /// never reaches the handler, so this also shows that the limiter is asked
    /// inside a request that got that far, and not instead of the check.
    /// </remarks>
    private static async Task<HttpResponseMessage> RefusedAsync(HttpClient client)
    {
        var token = await OwnerApplication.AntiforgeryTokenAsync(client, LoginPath);
        HttpResponseMessage? response = null;

        for (var attempt = 1; attempt <= PermitLimit + 1; attempt++)
        {
            response = await OwnerApplication.SignInAsync(client, token, OwnerApplication.WrongPassword);
        }

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);

        return response;
    }
}
