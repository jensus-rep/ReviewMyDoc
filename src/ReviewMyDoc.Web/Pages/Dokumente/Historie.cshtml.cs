// The document history uses the default owner authorization policy. Review
// capabilities never grant access to this page or its resolved private names.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Core.Audit;
using ReviewMyDoc.Core.Documents;

namespace ReviewMyDoc.Web.Pages.Dokumente;

/// <summary>Shows the owner's readable history of a document.</summary>
public sealed class HistorieModel(DocumentHistoryService history) : PageModel
{
    /// <summary>The document and its newest-first events.</summary>
    public DocumentHistory History { get; private set; } = null!;

    /// <summary>Loads an existing document, refusing malformed or unknown identifiers.</summary>
    public async Task<IActionResult> OnGetAsync(string documentId, CancellationToken ct)
    {
        DocumentIdentifier id;
        try { id = new DocumentIdentifier(documentId); }
        catch (ArgumentException) { return NotFound(); }
        var loaded = await history.LoadAsync(id, ct);
        if (loaded is null) { return NotFound(); }
        History = loaded;
        return Page();
    }
}
