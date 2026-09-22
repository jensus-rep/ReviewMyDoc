// Error page shown when a request fails outside the handling of a single page.
// It names the request id so a report can be matched with the log, and nothing
// else: an error page must never leak details about the failure.

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ReviewMyDoc.Web.Pages;

/// <summary>Page model of the error page.</summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ErrorModel : PageModel
{
    /// <summary>Id of the failed request, taken from the current activity.</summary>
    public string? RequestId { get; private set; }

    /// <summary>Whether a request id is available and can be shown.</summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>Collects the id of the current request.</summary>
    public void OnGet() => RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
}
