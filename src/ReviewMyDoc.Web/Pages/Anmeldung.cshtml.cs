// Page model of the sign-in of the owner. It is the one page of the application
// an anonymous visitor may see, and it is the only place a password is ever
// looked at. It does three things in this order: ask the limiter, check the
// password, write the cookie. The order matters, because a refused attempt must
// not reach the password hash at all.

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Web.Pages;

/// <summary>
/// Model of the page at <c>/anmeldung</c>.
/// </summary>
/// <remarks>
/// <see cref="AllowAnonymousAttribute"/> is the deliberate exception from the
/// fallback policy of the application, and the reason is the obvious one:
/// without it nobody could ever sign in. Every page that carries this attribute
/// is listed in <c>AuthorizationDefaultTests</c>, so an exception cannot be made
/// quietly; see
/// <see cref="OwnerAuthenticationServiceCollectionExtensions.AddOwnerAuthentication"/>.
/// </remarks>
[AllowAnonymous]
public sealed class AnmeldungModel : PageModel
{
    /// <summary>
    /// The one answer to every failed attempt.
    /// </summary>
    /// <remarks>
    /// It names no reason. A wrong password, an empty field, a configuration
    /// without a hash and a hash that cannot be read all end here, because every
    /// difference between them would tell whoever is trying something they did
    /// not know before.
    /// </remarks>
    public const string FailureMessage = "Die Anmeldung ist fehlgeschlagen. Bitte versuchen Sie es erneut.";

    /// <summary>What the page says after an attempt the limiter refused.</summary>
    /// <remarks>
    /// It names when it works again and not how many attempts there were, which
    /// would be a counter somebody could read the limit off.
    /// </remarks>
    public const string RateLimitedMessage =
        "Zu viele Anmeldeversuche. Bitte warten Sie eine Minute und versuchen Sie es erneut.";

    /// <summary>Where a successful sign-in leads without a return address.</summary>
    public const string Home = "/";

    private readonly OwnerPasswordCheck _passwordCheck;
    private readonly PartitionedRateLimiter<HttpContext> _rateLimiter;
    private readonly ILogger<AnmeldungModel> _logger;

    /// <summary>Takes the services of one request.</summary>
    /// <param name="passwordCheck">Checks the password against the configured hash.</param>
    /// <param name="rateLimiter">
    /// The limiter registered under <see cref="LoginRateLimit.Name"/>. Named and
    /// not resolved by type, so this page cannot quietly spend the budget of
    /// another one.
    /// </param>
    /// <param name="logger">The log. Neither the password nor the hash reaches it.</param>
    public AnmeldungModel(
        OwnerPasswordCheck passwordCheck,
        [FromKeyedServices(LoginRateLimit.Name)] PartitionedRateLimiter<HttpContext> rateLimiter,
        ILogger<AnmeldungModel> logger)
    {
        _passwordCheck = passwordCheck;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <summary>
    /// The password that was typed in.
    /// </summary>
    /// <remarks>
    /// It never goes back into the page: the field partial renders no value for
    /// a password, and this property is deliberately not read while rendering.
    /// </remarks>
    [BindProperty]
    public string? Password { get; set; }

    /// <summary>
    /// The page the visitor wanted to see, put into the query by the cookie
    /// middleware.
    /// </summary>
    /// <remarks>
    /// Only a local address is ever followed, so the form of this application
    /// cannot be used to send somebody to another site.
    /// </remarks>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>The sentence above the form, or nothing before the first attempt.</summary>
    public string? Message { get; private set; }

    /// <summary>Shows the empty form.</summary>
    /// <returns>
    /// The page, or the way onward for somebody who is signed in already: a
    /// second sign-in form for a session that exists is only a way to lose it.
    /// </returns>
    public IActionResult OnGet()
    {
        if (User.IsInRole(OwnerAuthentication.Role))
        {
            return Redirect(SafeReturnUrl());
        }

        return Page();
    }

    /// <summary>Checks the password and signs the owner in.</summary>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    /// <returns>
    /// The redirect onwards after a successful attempt, otherwise the page: with
    /// 200 and the failure message after a wrong password, with 429 and
    /// <c>Retry-After</c> after a refusal of the limiter.
    /// </returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // The limiter first, before the password is looked at. Checking a hash
        // costs work on purpose, and an attempt that is refused anyway must not
        // buy that work from the server.
        using var lease = await _rateLimiter.AcquireAsync(HttpContext, cancellationToken: cancellationToken);

        if (!lease.IsAcquired)
        {
            RateLimitRefusal.Answer(Response, lease, RateLimitPartitions.Window);

            Message = RateLimitedMessage;

            _logger.LogWarning(
                "A sign-in attempt from {ClientAddressHash} was refused by the rate limiter.",
                ClientAddressHash.PartitionOf(HttpContext));

            // Page() keeps the status set above, so the visitor gets the frame,
            // the sentence and the form back while the server answers 429.
            return Page();
        }

        if (!_passwordCheck.Matches(Password))
        {
            Message = FailureMessage;

            // The hashed address and nothing else. Not the password, not its
            // length, not the hash it was checked against; see
            // docs/Konventionen.md, section Code.
            _logger.LogWarning(
                "A sign-in attempt from {ClientAddressHash} failed.",
                ClientAddressHash.PartitionOf(HttpContext));

            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            OwnerAuthentication.CreatePrincipal(),
            new AuthenticationProperties
            {
                // A session cookie, gone when the browser is closed. The owner
                // works on one machine and the application holds documents that
                // are not meant to be one click away on a borrowed computer.
                IsPersistent = false,
            });

        _logger.LogInformation(
            "The owner signed in from {ClientAddressHash}.",
            ClientAddressHash.PartitionOf(HttpContext));

        return Redirect(SafeReturnUrl());
    }

    /// <summary>The address to go to after a sign-in.</summary>
    /// <returns>
    /// The return address when it points into this application, otherwise the
    /// start page.
    /// </returns>
    private string SafeReturnUrl() =>
        !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Home;
}
