// Computes the owner's pipeline from assignments and an injectable clock.
// No second persisted status can drift away from the review aggregates.
using ReviewMyDoc.Core.Documents;

namespace ReviewMyDoc.Core.Reviews;

/// <summary>The four review stages and their time-dependent presentation.</summary>
public sealed class ReviewPipeline(IDocumentStore documents, ReviewStore reviews, TimeProvider clock)
{
    /// <summary>Loads the current pipeline; closed work stays visible for thirty days.</summary>
    public async Task<IReadOnlyList<ReviewAssignment>> LoadAsync(CancellationToken ct)
    {
        var result = new List<ReviewAssignment>();
        // Prefix scans are appropriate for hundreds of documents. Beyond that,
        // introduce a rebuilt projection rather than an authoritative status copy.
        foreach (var document in await documents.ListDocumentsAsync(ct))
        {
            result.AddRange((await reviews.ListAsync(document.Id, ct)).Select(r => r.Review));
        }
        return result.Where(r => r.State != "Revoked" && (r.State != "Accepted" ||
            (r.AcceptedAt ?? r.ReturnedAt ?? r.CreatedAt) >= clock.GetUtcNow().AddDays(-30)))
            .OrderBy(r => r.DueAt).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Exactly one stage per active assignment; questions take owner priority.</summary>
    public static string Stage(ReviewAssignment review) => review.State switch
    {
        "Sent" => "Draußen",
        "Returned" when review.Passages.Any(p => ReviewService.IsOpen(p) && p.FeedbackKind == "Question") => "Bei dir",
        "Returned" when review.Passages.Any(ReviewService.IsOpen) => "Zurück",
        "Returned" => "Bei dir",
        "Accepted" => "Erledigt",
        _ => "Gesammelt"
    };

    /// <summary>Whether an outstanding response has missed its deadline.</summary>
    public bool IsOverdue(ReviewAssignment review) => review.State == "Sent" && review.DueAt < clock.GetUtcNow();

    /// <summary>Readable remaining time based on the same clock as overdue sorting.</summary>
    public string Remaining(ReviewAssignment review) => review.DueAt is not { } due ? "Keine Frist" :
        due < clock.GetUtcNow() ? "Frist überschritten" : $"Noch {Math.Max(1, (int)Math.Ceiling((due - clock.GetUtcNow()).TotalDays))} Tage";
}
