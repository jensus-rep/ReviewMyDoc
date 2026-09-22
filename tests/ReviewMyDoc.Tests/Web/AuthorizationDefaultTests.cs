// Checks the rule that protects every page of this application: not a list of
// protected pages, but a default that holds for every endpoint nobody said
// anything about. The last test in this file is the one that will still matter
// in a year: it walks every page the application routes and demands that each
// one either wants the role owner or stands in the short, named list of
// exceptions below. A page added later and forgotten is protected; a page
// opened deliberately fails this test until somebody writes it down here.

using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ReviewMyDoc.Web.Pages;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Tests of the default authorization of the application.</summary>
public sealed class AuthorizationDefaultTests
{
    /// <summary>
    /// The pages that may be reached without signing in, and why.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>/Anmeldung</c>: without it nobody could ever sign in.
    /// </para>
    /// <para>
    /// <c>/Error</c>: a request that failed before anybody signed in is
    /// re-executed onto this page, and a protected error page would answer a
    /// failure with a redirect to the form. The page shows a request id and
    /// nothing else.
    /// </para>
    /// <para>
    /// The review view under <c>/review/{token}</c> of Epic 2 will be the third
    /// entry. It proves itself with the token in the address instead of with a
    /// sign-in, and whoever adds it adds it here, in this list, with its reason.
    /// </para>
    /// </remarks>
    private static readonly string[] PagesWithoutOwner = ["/Anmeldung", "/Error"];

    // The rule itself. A fallback policy applies to every endpoint that carries
    // no authorization of its own, which is what makes it a default instead of
    // an enumeration.
    [Fact]
    public void Every_endpoint_demands_the_role_owner_by_default()
    {
        using var application = new OwnerApplication();

        var options = application.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var fallback = options.FallbackPolicy;

        Assert.NotNull(fallback);
        Assert.Contains(fallback!.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(
            fallback.Requirements.OfType<RolesAuthorizationRequirement>(),
            requirement => requirement.AllowedRoles.Contains(OwnerAuthentication.Role));
    }

    // The list of exceptions, checked against the application instead of
    // against itself. Whoever writes [AllowAnonymous] on a page has to name it
    // above, and whoever takes the attribute off a page has to take it out of
    // the list, so neither can happen unnoticed.
    [Fact]
    public void Only_the_named_pages_are_open_without_a_sign_in()
    {
        using var application = new OwnerApplication();

        var endpoints = application.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        var open = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<PageActionDescriptor>() is not null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.Metadata.GetMetadata<PageActionDescriptor>()!.ViewEnginePath)
            .Distinct()
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(PagesWithoutOwner.OrderBy(path => path, StringComparer.Ordinal), open);
    }

    // The same thing seen from outside: the pages of the application really are
    // unreachable without a session, and they answer with the way to the form
    // rather than with a bare refusal.
    [Theory]
    [InlineData("/")]
    [InlineData("/abmelden")]
    public async Task A_page_of_the_application_sends_an_anonymous_visitor_to_the_sign_in(string path)
    {
        using var application = new OwnerApplication();
        using var client = application.CreateAnonymousClient(followRedirects: false);

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var location = response.Headers.Location?.ToString() ?? string.Empty;

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains(OwnerAuthentication.LoginPath, location, StringComparison.Ordinal);

        // The way back, so a bookmark of a protected page leads there after the
        // sign-in instead of to the start page.
        Assert.Contains("ReturnUrl=", location, StringComparison.Ordinal);
    }

    // Deployed is where it counts, so the same question is asked again in an
    // environment that is not development. Nothing is open there that is not
    // open here.
    [Fact]
    public async Task Nothing_is_open_outside_development_either()
    {
        using var application = new OwnerApplication { EnvironmentName = Environments.Production };
        using var client = application.CreateAnonymousClient(followRedirects: false);

        var start = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var login = await client.GetAsync(OwnerAuthentication.LoginPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, start.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    // A deployed application without a password hash could be signed into by
    // nobody, and it would say so only to whoever tried. It does not start, and
    // the message names the key that is missing.
    [Fact]
    public void Outside_development_the_application_does_not_start_without_a_password_hash()
    {
        using var application = new OwnerApplication
        {
            EnvironmentName = Environments.Production,
            PasswordHash = string.Empty,
        };

        var failure = Assert.ThrowsAny<Exception>(() => application.CreateAnonymousClient());

        Assert.Contains(
            $"{OwnerOptions.SectionName}:{nameof(OwnerOptions.PasswordHash)}",
            Flatten(failure),
            StringComparison.Ordinal);
    }

    // In development the application starts without a hash, because a repository
    // that was only just cloned has to be able to start. Nobody gets in that
    // way: an attempt is answered like any other wrong password.
    [Fact]
    public async Task In_development_an_application_without_a_hash_lets_nobody_in()
    {
        using var application = new OwnerApplication { PasswordHash = string.Empty };
        using var client = application.CreateAnonymousClient();
        var token = await OwnerApplication.AntiforgeryTokenAsync(client, OwnerAuthentication.LoginPath);

        var response = await OwnerApplication.SignInAsync(client, token, OwnerApplication.Password);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            AnmeldungModel.FailureMessage,
            html,
            StringComparison.Ordinal);
    }

    /// <summary>All messages of an exception and of everything inside it.</summary>
    /// <param name="exception">The exception the start failed with.</param>
    /// <returns>The messages, one after another.</returns>
    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();

        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" ", messages);
    }
}
