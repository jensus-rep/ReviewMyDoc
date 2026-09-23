// The pipeline is a projection over real assignments, not a second state store.
// Draft collections remain accessible beside the four review stages.

using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Reviews;

namespace ReviewMyDoc.Web.Pages;

/// <summary>Page model of the start page.</summary>
public sealed class IndexModel(DocumentService documents, ReviewService reviews) : PageModel
{
    /// <summary>All persisted assignments available to the owner.</summary>
    public IReadOnlyList<ReviewAssignment> Reviews { get; private set; } = [];

    /// <summary>Loads the pipeline from its source aggregates.</summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var result = new List<ReviewAssignment>();
        foreach (var document in await documents.ListDocumentsAsync(ct))
        {
            result.AddRange((await reviews.ListAsync(document.Id, ct)).Select(r => r.Review));
        }
        Reviews = result.OrderBy(r => r.DueAt).ToArray();
    }

    /// <summary>The stage follows the review and its unresolved comments.</summary>
    public static string Stage(ReviewAssignment review) => review.State switch
    {
        "Sent" => "Draußen",
        "Returned" when review.Passages.Any(p => !string.IsNullOrWhiteSpace(p.Feedback) && !p.Resolved) => "Zurück",
        "Returned" => "Bei dir",
        "Accepted" => "Erledigt",
        _ => "Gesammelt",
    };
}
