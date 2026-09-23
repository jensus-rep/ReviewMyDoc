// The pipeline is a projection over real assignments, not a second state store.
// Draft collections remain accessible beside the four review stages.

using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Reviews;

namespace ReviewMyDoc.Web.Pages;

/// <summary>Page model of the start page.</summary>
public sealed class IndexModel(ReviewPipeline pipeline) : PageModel
{
    /// <summary>All persisted assignments available to the owner.</summary>
    public IReadOnlyList<ReviewAssignment> Reviews { get; private set; } = [];

    /// <summary>Loads the pipeline from its source aggregates.</summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Reviews = await pipeline.LoadAsync(ct);
    }

    /// <summary>The stage follows the review and its unresolved comments.</summary>
    public static string Stage(ReviewAssignment review) => ReviewPipeline.Stage(review);
    /// <summary>Time remaining from the application clock.</summary>
    public string Remaining(ReviewAssignment review) => pipeline.Remaining(review);
    /// <summary>Overdue state from the application clock.</summary>
    public bool IsOverdue(ReviewAssignment review) => pipeline.IsOverdue(review);
}
