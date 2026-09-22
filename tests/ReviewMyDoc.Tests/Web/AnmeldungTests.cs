// Checks the sign-in of the owner, criterion by criterion: the one message that
// says nothing, the refusal of the limiter inside the frame of the page, the
// antiforgery token, the password that never comes back and the log that never
// sees it. Every test starts an application of its own; the limiter counts
// across requests, which is its purpose, so a shared application would let one
// test spend the budget of the next.

using System.Net;
using ReviewMyDoc.Web.Pages;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Integration tests of <see cref="AnmeldungModel"/> and the sign-out.</summary>
public sealed class AnmeldungTests
{
    /// <summary>A page that only the owner may see.</summary>
    private const string ProtectedPath = "/";

    // The form is the one page anybody may open, and it carries the token
    // without which no post of this application is accepted.
    [Fact]
    public async Task The_sign_in_page_is_open_and_carries_an_antiforgery_token()
    {
        using var application = new OwnerApplication();
        using var client = application.CreateAnonymousClient();

        var response = await client.GetAsync(OwnerAuthentication.LoginPath, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"password\"", html, StringComparison.Ordinal);
    }

    // A post without the token never reaches the page model, so nothing it
    // guards is reachable by a form on another site.
    [Fact]
    public async Task A_sign_in_without_the_antiforgery_token_is_refused()
    {
        using var application = new OwnerApplication();
        using var client = application.CreateAnonymousClient();

        var form = new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("Password", OwnerApplication.Password)]);

        var response = await client.PostAsync(
            OwnerAuthentication.LoginPath,
            form,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The right password ends on the page the visitor wanted, and the session
    // that follows really opens a protected page.
    [Fact]
    public async Task The_right_password_signs_the_owner_in()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();

        var response = await client.GetAsync(ProtectedPath, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Abmelden", html, StringComparison.Ordinal);
    }

    // The heart of the first criterion: the answer names no reason. It is the
    // same sentence for a wrong password as for an empty field, and the page
    // comes back with 200 rather than sending the visitor anywhere.
    [Theory]
    [InlineData(OwnerApplication.WrongPassword)]
    [InlineData("")]
    public async Task A_failed_attempt_answers_one_sentence_that_names_no_reason(string password)
    {
        using var application = new OwnerApplication();
        using var client = application.CreateAnonymousClient();
        var token = await OwnerApplication.AntiforgeryTokenAsync(client, OwnerAuthentication.LoginPath);

        var response = await OwnerApplication.SignInAsync(client, token, password);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(AnmeldungModel.FailureMessage, html, StringComparison.Ordinal);

        // Nothing that would say what was wrong or whether there is anything
        // here to guess at.
        Assert.DoesNotContain("Hash", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("konfiguriert", html, StringComparison.OrdinalIgnoreCase);
    }

    // A refused attempt must not hand the password back to the browser, where
    // it would stand in the markup, in a cache and in the next screenshot.
    [Fact]
    public async Task A_failed_attempt_does_not_give_the_password_back()
    {
        const string typed = "ein Passwort, das nirgends wieder auftauchen darf";

        using var application = new OwnerApplication();
        using var client = application.CreateAnonymousClient();
        var token = await OwnerApplication.AntiforgeryTokenAsync(client, OwnerAuthentication.LoginPath);

        var response = await OwnerApplication.SignInAsync(client, token, typed);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(typed, html, StringComparison.Ordinal);
    }

    // The criterion the check page under Pages/Pruefung/ used to show, now shown
    // by the sign-in itself and with the number the application really
    // documents: the eleventh attempt within a minute is refused, and refused
    // means the page in its frame with 429 and Retry-After, never an empty
    // answer.
    [Fact]
    public async Task The_eleventh_attempt_within_a_minute_answers_429_in_the_frame_of_the_page()
    {
        const string returnUrl = "/dokumente";

        using var application = new OwnerApplication();
        using var client = application.CreateAnonymousClient();
        var path = $"{OwnerAuthentication.LoginPath}?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
        var token = await OwnerApplication.AntiforgeryTokenAsync(client, path);

        for (var attempt = 1; attempt <= LoginRateLimit.DefaultPermitLimit; attempt++)
        {
            var allowed = await PostAsync(client, path, token, OwnerApplication.WrongPassword);

            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var refused = await PostAsync(client, path, token, OwnerApplication.WrongPassword);
        var html = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The status and the header.
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.NotNull(refused.Headers.RetryAfter);

        // Not an empty answer, but the page in the frame of the application.
        Assert.Equal("text/html", refused.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<html lang=\"de\">", html, StringComparison.Ordinal);
        Assert.Contains("class=\"app__main\" id=\"inhalt\"", html, StringComparison.Ordinal);
        Assert.Contains("Hauptnavigation", html, StringComparison.Ordinal);
        Assert.Contains("/components/tokens/tokens.css", html, StringComparison.Ordinal);

        // The sentence, the form and what the visitor was on their way to. The
        // password is the one thing that is deliberately not given back, see the
        // test above, so the return address is what a refusal must not cost.
        Assert.Contains(AnmeldungModel.RateLimitedMessage, html, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Contains($"ReturnUrl={Uri.EscapeDataString(returnUrl)}", html, StringComparison.Ordinal);
    }

    // A refusal of the limiter says nothing about the password either. Whoever
    // guesses must not be able to read out of the eleventh answer whether the
    // tenth was close.
    [Fact]
    public async Task A_refused_attempt_with_the_right_password_signs_nobody_in()
    {
        using var application = new OwnerApplication { LoginPermitLimit = 1 };
        using var client = application.CreateAnonymousClient();
        var token = await OwnerApplication.AntiforgeryTokenAsync(client, OwnerAuthentication.LoginPath);

        await OwnerApplication.SignInAsync(client, token, OwnerApplication.WrongPassword);
        var refused = await OwnerApplication.SignInAsync(client, token, OwnerApplication.Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        var protectedPage = await client.GetAsync(ProtectedPath, TestContext.Current.CancellationToken);

        // The same client, and it still has no session: the attempt was refused
        // before the password was ever looked at.
        Assert.Contains(
            "Anmeldung",
            await protectedPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }

    // The password and its hash are the two things that must never be readable
    // anywhere afterwards. A log file is read by more people than a
    // configuration, and it is copied, shipped and kept.
    [Fact]
    public async Task Neither_the_password_nor_the_hash_reaches_the_log()
    {
        using var application = new OwnerApplication();
        using var client = application.CreateAnonymousClient();
        var token = await OwnerApplication.AntiforgeryTokenAsync(client, OwnerAuthentication.LoginPath);

        await OwnerApplication.SignInAsync(client, token, OwnerApplication.WrongPassword);
        await OwnerApplication.SignInAsync(client, token, OwnerApplication.Password);

        var log = string.Join("\n", application.LogMessages);

        Assert.NotEmpty(application.LogMessages);
        Assert.DoesNotContain(OwnerApplication.Password, log, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnerApplication.WrongPassword, log, StringComparison.Ordinal);
        Assert.DoesNotContain(application.PasswordHash, log, StringComparison.Ordinal);
    }

    // The sign-out takes a form and nothing else, so no link, no prefetch and no
    // image tag on another site can end a session.
    [Fact]
    public async Task The_sign_out_answers_no_get()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();

        var response = await client.GetAsync(OwnerAuthentication.LogoutPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // After the sign-out the session is really gone, and the protected page is
    // the sign-in form again.
    [Fact]
    public async Task The_sign_out_ends_the_session()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();
        var page = await client.GetStringAsync(ProtectedPath, TestContext.Current.CancellationToken);

        var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>(
                "__RequestVerificationToken",
                OwnerApplication.TokenIn(page)),
        ]);

        var response = await client.PostAsync(
            OwnerAuthentication.LogoutPath,
            form,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.EndsWith(
            OwnerAuthentication.LoginPath,
            response.RequestMessage?.RequestUri?.AbsolutePath ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        var afterwards = await client.GetStringAsync(ProtectedPath, TestContext.Current.CancellationToken);

        Assert.Contains("type=\"password\"", afterwards, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        string token,
        string password)
    {
        var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Password", password),
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        ]);

        return client.PostAsync(path, form, TestContext.Current.CancellationToken);
    }
}
