// Page model of the sign-out: it drops the cookie and sends the visitor to the
// sign-in form. There is only a post handler, so a link, a prefetch of the
// browser or an image tag on another site cannot sign anybody out. Taken from
// Atelier, where the same page is built the same way and argued.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Web.Pages;

/// <summary>
/// Model of the page at <c>/abmelden</c>, which takes the form in the header of
/// the application and nothing else.
/// </summary>
/// <remarks>
/// The page carries no <c>[AllowAnonymous]</c>, so the fallback policy applies
/// and only a signed-in owner can reach it. Somebody who is not signed in has
/// nothing to sign out of, and the sign-in form is what they need instead, which
/// is exactly where the challenge sends them.
/// </remarks>
public sealed class AbmeldenModel : PageModel
{
    private readonly ILogger<AbmeldenModel> _logger;

    /// <summary>Takes the log of the request.</summary>
    /// <param name="logger">The log. The event is noted, nothing about it.</param>
    public AbmeldenModel(ILogger<AbmeldenModel> logger) => _logger = logger;

    /// <summary>Refuses to sign anybody out on a GET.</summary>
    /// <returns>405, because this page takes a form and nothing else.</returns>
    /// <remarks>
    /// Razor Pages renders a page without a matching handler, so without this
    /// method a GET would answer with an empty page and 200 and would look like
    /// a sign-out that did not happen.
    /// </remarks>
    public IActionResult OnGet() => StatusCode(StatusCodes.Status405MethodNotAllowed);

    /// <summary>Drops the cookie of the current session.</summary>
    /// <returns>The way to the sign-in form.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation(
            "The owner signed out from {ClientAddressHash}.",
            ClientAddressHash.PartitionOf(HttpContext));

        return Redirect(OwnerAuthentication.LoginPath);
    }
}
