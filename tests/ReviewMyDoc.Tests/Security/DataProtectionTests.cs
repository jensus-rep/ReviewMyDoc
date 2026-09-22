// Checks the assurance that the Data Protection registration exists for: the
// keys outlive the application. A test that only asserted "a key ring is
// configured" would be green with the default one too, and the default one is
// exactly the one that is thrown away at every restart. So every test here
// starts the application twice and asks the second one to read what the first
// one wrote.

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReviewMyDoc.Infrastructure.Security;
using ReviewMyDoc.Infrastructure.Storage;
using ReviewMyDoc.Web.Pages;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Tests.Security;

/// <summary>
/// Integration tests of
/// <see cref="DataProtectionServiceCollectionExtensions"/>.
/// </summary>
public sealed class DataProtectionTests : IDisposable
{
    /// <summary>The purpose the test protects with; any fixed string does.</summary>
    private const string Purpose = "ReviewMyDoc.Tests";

    /// <summary>
    /// The sign-in page, used here because an antiforgery token needs a form to
    /// be issued into and this is the form anybody can reach without a session.
    /// </summary>
    private const string FormPath = OwnerAuthentication.LoginPath;

    private readonly string _keysPath;

    /// <summary>
    /// Gives this test class a key directory of its own.
    /// </summary>
    /// <remarks>
    /// A directory per run, as docs/Konventionen.md demands for the storage
    /// tests and for the same reason: two worktrees running their tests at the
    /// same time must not write into one key ring.
    /// </remarks>
    public DataProtectionTests()
    {
        _keysPath = Path.Combine(Path.GetTempPath(), "reviewmydoc-keys-" + Guid.NewGuid().ToString("n"));
    }

    // The assurance itself. The first application protects a text, then it is
    // gone; a second one, started from nothing but the same configuration, reads
    // it back. Without a persisted key ring the second one would throw, because
    // it would have minted a key of its own.
    [Fact]
    public void A_protected_text_survives_a_restart_of_the_application()
    {
        string protectedText;

        using (var before = Host())
        {
            protectedText = Protector(before).Protect("Sitzung des Eigentümers");
        }

        using var after = Host();

        Assert.Equal("Sitzung des Eigentümers", Protector(after).Unprotect(protectedText));
    }

    // Said the other way round, so that the test above cannot be green for the
    // wrong reason: a second application with a key ring of its own really
    // cannot read the text. This is what the registration prevents.
    [Fact]
    public void A_text_protected_with_another_key_ring_cannot_be_read()
    {
        string protectedText;

        using (var before = Host())
        {
            protectedText = Protector(before).Protect("Sitzung des Eigentümers");
        }

        var otherPath = Path.Combine(Path.GetTempPath(), "reviewmydoc-keys-" + Guid.NewGuid().ToString("n"));

        try
        {
            using var stranger = Host(otherPath);

            Assert.ThrowsAny<Exception>(() => Protector(stranger).Unprotect(protectedText));
        }
        finally
        {
            Delete(otherPath);
        }
    }

    // The keys really lie in the configured directory and nowhere else. This is
    // what an operator has to be able to back up, and what docs/Betrieb.md points
    // at.
    [Fact]
    public void The_keys_lie_in_the_configured_directory()
    {
        using var host = Host();

        Protector(host).Protect("etwas");

        Assert.True(Directory.Exists(_keysPath), $"The key directory {_keysPath} was not created.");
        Assert.NotEmpty(Directory.GetFiles(_keysPath, "key-*.xml"));
    }

    // A restart must not mint a second key. One key ring, one key, and the same
    // one afterwards: that is the difference between a key ring that is kept and
    // one that is merely written down somewhere.
    [Fact]
    public void A_restart_adds_no_second_key()
    {
        using (var before = Host())
        {
            Protector(before).Protect("etwas");
        }

        var afterFirstStart = Directory.GetFiles(_keysPath, "key-*.xml");

        using (var after = Host())
        {
            Protector(after).Protect("etwas anderes");
        }

        Assert.Equal(afterFirstStart, Directory.GetFiles(_keysPath, "key-*.xml"));
    }

    // The choice of the place is the choice of the object store, not a second
    // switch. Provider "Directory" with a connection configured still means the
    // directory, for the keys exactly as for the documents.
    [Fact]
    public void The_directory_is_used_when_the_storage_is_the_directory()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Storage:Provider"] = nameof(StorageProvider.Directory),
            ["Storage:Blob:ConnectionString"] = "UseDevelopmentStorage=true",
            ["DataProtection:KeysPath"] = _keysPath,
        };

        using var host = Host(settings);

        Protector(host).Protect("etwas");

        Assert.NotEmpty(Directory.GetFiles(_keysPath, "key-*.xml"));
    }

    // Azure demanded without a connection is broken configuration, and it stops
    // the start instead of quietly writing the keys onto a disk that the next
    // deployment wipes. That is the one place where this registration behaves
    // differently from the object store, and it does so on purpose.
    [Fact]
    public void Azure_without_a_connection_stops_the_start()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = nameof(StorageProvider.Blob),
                ["Storage:Blob:ServiceUri"] = string.Empty,
                ["Storage:Blob:ConnectionString"] = string.Empty,
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(
            () => services.AddDataProtectionKeys(configuration));

        Assert.Contains("Storage:Blob:ServiceUri", error.Message, StringComparison.Ordinal);
        Assert.Contains("docs/Betrieb.md", error.Message, StringComparison.Ordinal);
    }

    // The same assurance once more, but as a visitor meets it: an antiforgery
    // token is issued, the application is restarted, and the form that was on
    // screen the whole time still posts. Without a persisted key ring this would
    // be 400, and that is what every open form in the application would get
    // after a deployment or a restart in Azure.
    [Fact]
    public async Task A_form_that_was_open_during_a_restart_can_still_be_posted()
    {
        string token;
        string cookie;

        using (var before = HostIn(Environments.Development))
        using (var client = before.CreateClient())
        {
            var page = await client.GetAsync(FormPath, TestContext.Current.CancellationToken);
            var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            token = Match(html);
            cookie = Assert.Single(page.Headers.GetValues("Set-Cookie")).Split(';')[0];
        }

        using var after = HostIn(Environments.Development);
        using var restarted = after.CreateClient();

        var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Password", "vor dem Neustart getippt"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        ]);

        using var request = new HttpRequestMessage(HttpMethod.Post, FormPath) { Content = form };
        request.Headers.Add("Cookie", cookie);

        var response = await restarted.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The password of this post is wrong on purpose, so the answer is the
        // form with its one sentence. What matters is that it is that and not
        // 400: a token from before the restart was still readable afterwards.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(AnmeldungModel.FailureMessage, body, StringComparison.Ordinal);
    }

    /// <summary>Removes the key directory of this test class.</summary>
    public void Dispose() => Delete(_keysPath);

    private static string Match(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(match.Success, "The sign-in form carries no antiforgery token.");

        return match.Groups[1].Value;
    }

    private WebApplicationFactory<Program> HostIn(string environment) =>
        new ConfiguredApplication(
            new Dictionary<string, string?>
            {
                ["Storage:Provider"] = nameof(StorageProvider.Directory),
                ["DataProtection:KeysPath"] = _keysPath,
            },
            environment);

    private static IDataProtector Protector(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

    private WebApplicationFactory<Program> Host(string? keysPath = null) =>
        Host(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = nameof(StorageProvider.Directory),
            ["DataProtection:KeysPath"] = keysPath ?? _keysPath,
        });

    private static WebApplicationFactory<Program> Host(Dictionary<string, string?> settings) =>
        new ConfiguredApplication(settings);

    private static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    /// <summary>The application under test with a configuration of its own.</summary>
    /// <remarks>
    /// The settings are handed over with <c>UseSetting</c> and not with
    /// <c>ConfigureAppConfiguration</c>, and that is not a matter of taste.
    /// A source added in <c>ConfigureAppConfiguration</c> reaches the
    /// configuration only after Program.cs has run, so a registration that reads
    /// its section while the application starts, as the key ring does and has
    /// to, would still see the values of appsettings.json. Both applications of
    /// a restart test would then share one default directory and the test would
    /// be green without proving anything.
    /// </remarks>
    private sealed class ConfiguredApplication : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _settings;
        private readonly string? _environment;

        public ConfiguredApplication(Dictionary<string, string?> settings, string? environment = null)
        {
            _settings = settings;
            _environment = environment;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (_environment is not null)
            {
                builder.UseEnvironment(_environment);
            }

            foreach (var setting in _settings)
            {
                builder.UseSetting(setting.Key, setting.Value);
            }
        }
    }
}
