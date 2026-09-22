// Page model of the list of documents at /dokumente. It reads the list through
// DocumentService.ListDocumentsAsync, which lists the object store by prefix
// instead of keeping an index of its own - see docs/Datenmodell.md, section
// Warum ohne Datenbank - and turns it into the rows the building block
// components/rows shows, newest change first.

using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Web.Pages.Shared.Ui;

namespace ReviewMyDoc.Web.Pages.Dokumente;

/// <summary>Model of the page at <c>/dokumente</c>.</summary>
public sealed class IndexModel : PageModel
{
    private readonly DocumentService _documents;

    /// <summary>Takes the service the list is read through.</summary>
    /// <param name="documents">Where the documents of the application come from.</param>
    public IndexModel(DocumentService documents) => _documents = documents;

    /// <summary>
    /// Every document there is, newest change first, already turned into the
    /// rows the page renders. Empty items mean the empty state, not a page that
    /// forgot how to render.
    /// </summary>
    public RowsModel Rows { get; private set; } = new([]);

    /// <summary>Loads the list.</summary>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var documents = await _documents.ListDocumentsAsync(cancellationToken);

        Rows = new RowsModel([.. documents.Select(RowOf)]);
    }

    /// <summary>Turns one document into the row that shows it.</summary>
    /// <remarks>
    /// No action: the outline page a row would lead to,
    /// <c>/dokumente/{documentId}</c>, is the next task and does not exist yet.
    /// A row without one is still a complete entry of the list, as
    /// components/rows/README.md allows.
    /// </remarks>
    private static RowModel RowOf(Document document) => new(document.Title)
    {
        Text = $"{DocumentStateLabel.Of(document.State)} · zuletzt geändert am "
            + document.UpdatedAt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
    };
}
