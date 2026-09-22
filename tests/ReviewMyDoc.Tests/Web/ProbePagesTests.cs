// Checks that the check pages under Pages/Pruefung/ are reachable while
// developing and have no address at all in a deployed application. A page that
// exists to demonstrate a refusal is useful on a developer machine and is
// nothing but surface on the internet.

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Integration tests of <see cref="ReviewMyDoc.Web.Security.ProbePagesConvention"/>.</summary>
public sealed class ProbePagesTests
{
    private const string ProbePath = "/pruefung/ratenbegrenzung";

    // In development the page answers, otherwise nothing could be shown on it.
    [Fact]
    public async Task The_check_page_answers_while_developing()
    {
        using var application = new Application(Environments.Development);
        using var client = application.CreateClient();

        var response = await client.GetAsync(ProbePath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Deployed it is gone, and gone means 404 like any address that was never
    // there, not a page that merely refuses.
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task The_check_page_has_no_address_outside_development(string environment)
    {
        using var application = new Application(environment);
        using var client = application.CreateClient();

        var response = await client.GetAsync(ProbePath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>The application under test in one named environment.</summary>
    public sealed class Application : WebApplicationFactory<Program>
    {
        private readonly string _environment;

        /// <summary>Takes the environment the application should start in.</summary>
        /// <param name="environment">For example <c>Production</c>.</param>
        public Application(string environment) => _environment = environment;

        /// <inheritdoc />
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment(_environment);
        }
    }
}
