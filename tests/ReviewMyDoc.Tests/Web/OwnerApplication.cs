// The application under test, started the same way for every integration test:
// in a named environment, with a password hash of its own and with a log that
// can be read back afterwards. It exists because every page of ReviewMyDoc
// demands the role owner, so a test that wants to see a page has to sign in
// first, and a test that signs in differently from the next one would prove
// something about itself instead of about the application.

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Tests.Web;

/// <summary>The web application in a test, with a known password.</summary>
public class OwnerApplication : WebApplicationFactory<Program>
{
    /// <summary>The password the application under test accepts.</summary>
    /// <remarks>
    /// A sentence with a space and an umlaut, because a password is not a word
    /// and the form has to carry it through form encoding unharmed.
    /// </remarks>
    public const string Password = "richtiges Kennwort für den Test";

    /// <summary>A password that is not <see cref="Password"/>.</summary>
    public const string WrongPassword = "falsches Kennwort";

    /// <summary>The address of the sign-in page.</summary>
    public const string LoginPath = OwnerAuthentication.LoginPath;

    private readonly RecordingLoggerProvider _log = new();
    private readonly SemaphoreSlim _ownerClientGate = new(1, 1);
    private HttpClient? _ownerClient;

    /// <summary>
    /// The environment the application starts in. Named and not left to the
    /// test runner, because the environment decides whether a cookie may travel
    /// over http and whether a missing hash stops the start.
    /// </summary>
    public string EnvironmentName { get; init; } = Environments.Development;

    /// <summary>
    /// The hash of <see cref="Password"/>, built with the very hasher the
    /// command <c>passwort-hash</c> uses.
    /// </summary>
    /// <remarks>
    /// Built and not written down as a literal: the hash carries a random salt,
    /// so a literal would only show that one saved hash still works, while this
    /// shows that a hash created today does.
    /// </remarks>
    public string PasswordHash { get; init; } = PasswordHashCommand.HashOf(Password);

    /// <summary>
    /// How many sign-in attempts per minute the application under test allows,
    /// or zero for the limit the application itself documents.
    /// </summary>
    public int LoginPermitLimit { get; init; }

    /// <summary>Everything the application wrote to its log since it started.</summary>
    public IReadOnlyList<string> LogMessages => _log.Messages;

    /// <summary>Makes a client that carries no session.</summary>
    /// <param name="followRedirects">
    /// Whether a redirect is followed. A test that wants to see the redirect
    /// itself, and not where it leads, says <see langword="false"/>.
    /// </param>
    /// <returns>The client.</returns>
    public HttpClient CreateAnonymousClient(bool followRedirects = true) =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = followRedirects });

    /// <summary>
    /// The one signed-in client of this application, signed in on first use.
    /// </summary>
    /// <returns>The client. It belongs to the application and is not disposed by a test.</returns>
    /// <remarks>
    /// Shared on purpose. A test class that shares one application shares its
    /// rate limiter as well, and signing in once per test would spend the ten
    /// attempts a minute allows after ten tests and turn a green test class red
    /// as it grows. One session for the whole class is also what a person does.
    /// A test that wants a session of its own, or none, says so with
    /// <see cref="CreateOwnerClientAsync"/> or
    /// <see cref="CreateAnonymousClient"/>.
    /// </remarks>
    public async Task<HttpClient> OwnerClientAsync()
    {
        await _ownerClientGate.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            return _ownerClient ??= await CreateOwnerClientAsync();
        }
        finally
        {
            _ownerClientGate.Release();
        }
    }

    /// <summary>Makes a client that is signed in as the owner.</summary>
    /// <returns>The client, carrying the cookie of a real sign-in.</returns>
    /// <remarks>
    /// It really signs in, through the form, instead of putting a cookie
    /// together itself. A test that forged the cookie would keep passing after
    /// the sign-in stopped working.
    /// </remarks>
    public async Task<HttpClient> CreateOwnerClientAsync()
    {
        var client = CreateAnonymousClient();
        var token = await AntiforgeryTokenAsync(client, LoginPath);

        var response = await SignInAsync(client, token, Password);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        return client;
    }

    /// <summary>Posts the sign-in form.</summary>
    /// <param name="client">The client, which carries the antiforgery cookie.</param>
    /// <param name="token">The token out of the form.</param>
    /// <param name="password">What to type into the field.</param>
    /// <returns>The answer of the application.</returns>
    public static Task<HttpResponseMessage> SignInAsync(HttpClient client, string token, string password)
    {
        ArgumentNullException.ThrowIfNull(client);

        var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Password", password),
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        ]);

        return client.PostAsync(LoginPath, form, TestContext.Current.CancellationToken);
    }

    /// <summary>Reads the antiforgery token out of the form of a page.</summary>
    /// <param name="client">The client that also keeps the matching cookie.</param>
    /// <param name="path">The page carrying the form.</param>
    /// <returns>The value of the hidden field.</returns>
    public static async Task<string> AntiforgeryTokenAsync(HttpClient client, string path)
    {
        ArgumentNullException.ThrowIfNull(client);

        var html = await client.GetStringAsync(path, TestContext.Current.CancellationToken);

        return TokenIn(html);
    }

    /// <summary>Reads the antiforgery token out of rendered markup.</summary>
    /// <param name="html">The page.</param>
    /// <returns>The value of the hidden field.</returns>
    public static string TokenIn(string html)
    {
        var match = Regex.Match(
            html ?? string.Empty,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(match.Success, "The page carries no antiforgery token.");

        return match.Groups[1].Value;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(EnvironmentName);

        // UseSetting and not ConfigureAppConfiguration: the latter reaches the
        // configuration only after Program.cs has run, which is too late for
        // anything a registration reads while the application starts.
        builder.UseSetting($"{OwnerOptions.SectionName}:{nameof(OwnerOptions.PasswordHash)}", PasswordHash);

        if (LoginPermitLimit > 0)
        {
            builder.UseSetting(
                "RateLimits:Login:PermitLimit",
                LoginPermitLimit.ToString(CultureInfo.InvariantCulture));
        }

        builder.ConfigureLogging(logging => logging.AddProvider(_log));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ownerClient?.Dispose();
            _ownerClientGate.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>A log that keeps what was written to it.</summary>
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];
        private readonly Lock _gate = new();

        /// <summary>What the application logged, in order.</summary>
        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_gate)
                {
                    return [.. _messages];
                }
            }
        }

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);

        /// <inheritdoc />
        public void Dispose()
        {
        }

        private void Add(string message)
        {
            lock (_gate)
            {
                _messages.Add(message);
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly RecordingLoggerProvider _provider;
            private readonly string _category;

            public RecordingLogger(RecordingLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                ArgumentNullException.ThrowIfNull(formatter);

                // The formatted message, the raw state and the exception. A test
                // that asks whether a secret reached the log has to see all
                // three: a value can travel as a parameter that no message text
                // ever shows.
                _provider.Add(
                    $"{logLevel} {_category} {formatter(state, exception)} {state} {exception}");
            }
        }
    }
}
