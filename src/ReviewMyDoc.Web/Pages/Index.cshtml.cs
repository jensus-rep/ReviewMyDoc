// Start page of the application. It carries no content yet; it exists so the
// scaffolding can be started and checked end to end from the first day on.

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ReviewMyDoc.Web.Pages;

/// <summary>Page model of the start page.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Renders the page. There is nothing to prepare yet.</summary>
    public void OnGet()
    {
    }
}
