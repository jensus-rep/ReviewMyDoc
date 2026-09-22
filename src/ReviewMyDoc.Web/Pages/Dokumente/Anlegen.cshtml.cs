// Page model of the form that creates a document with a title and nothing
// else. A section, and a text for it, come later, on the outline page this one
// hands the owner on to; docs/Konzept.md keeps them apart on purpose - a
// document without sections is still a whole, valid state.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Core.Documents;

namespace ReviewMyDoc.Web.Pages.Dokumente;

/// <summary>Model of the page at <c>/dokumente/anlegen</c>.</summary>
public sealed class AnlegenModel : PageModel
{
    /// <summary>What the page says when the title is left empty.</summary>
    public const string EmptyTitleMessage = "Bitte einen Titel eingeben.";

    /// <summary>
    /// What the page says on the one failure <see cref="DocumentService.CreateDocumentAsync"/>
    /// can report: a drawn identifier that, against all odds, already names a
    /// document. Trying again draws a different one, so the answer is to ask
    /// for exactly that.
    /// </summary>
    public const string ConflictMessage = "Das Dokument konnte nicht angelegt werden. Bitte erneut versuchen.";

    /// <summary>
    /// The one owner this first version of the application has.
    /// docs/Konzept.md, section Offene Entscheidungen, keeps the field in the
    /// model so that a second owner later costs no migration; today there is
    /// exactly one, and this is its identifier.
    /// </summary>
    private const string OwnerId = "owner";

    private readonly DocumentService _documents;

    /// <summary>Takes the service the document is created through.</summary>
    /// <param name="documents">Where a new document is written to.</param>
    public AnlegenModel(DocumentService documents) => _documents = documents;

    /// <summary>The title as it was typed in.</summary>
    [BindProperty]
    public string? Title { get; set; }

    /// <summary>The message beside the field after a refused attempt, or nothing.</summary>
    public string? TitleError { get; private set; }

    /// <summary>Shows the empty form.</summary>
    public void OnGet()
    {
    }

    /// <summary>Checks the title and creates the document.</summary>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    /// <returns>
    /// A redirect to the outline of the new document at
    /// <c>/dokumente/{documentId}</c> on success - a page the next task builds,
    /// so nothing is created for it here beyond the address - otherwise the
    /// form with a message beside the field.
    /// </returns>
    /// <remarks>
    /// The empty title is caught here and never reaches the service, the same
    /// choice <see cref="DocumentService.AddSectionAsync"/> leaves to its
    /// callers: an empty heading is a form the page has to answer with a
    /// sentence beside the field, not an exception from the domain.
    /// </remarks>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            TitleError = EmptyTitleMessage;

            return Page();
        }

        var result = await _documents.CreateDocumentAsync(OwnerId, Title, cancellationToken);

        if (result is not DocumentResult.Success success)
        {
            TitleError = ConflictMessage;

            return Page();
        }

        return Redirect($"/dokumente/{success.Document.Id}");
    }
}
