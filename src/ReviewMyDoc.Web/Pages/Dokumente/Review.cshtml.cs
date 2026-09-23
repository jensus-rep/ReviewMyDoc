// The owner prepares an invitation and later resolves its returned comments.
// Every form carries the review ETag; issuance exposes the capability only once.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Markdown;
using ReviewMyDoc.Core.Reviews;

namespace ReviewMyDoc.Web.Pages.Dokumente;

/// <summary>Prepare and follow up one review assignment.</summary>
public sealed class ReviewModel(ReviewService reviews, IMarkdownRenderer renderer, IDocumentStore documents) : PageModel
{
    /// <summary>The assignment and concurrency token.</summary>
    public StoredReview Stored { get; private set; } = null!;
    /// <summary>A recoverable error shown alongside the form.</summary>
    public string? Message { get; private set; }
    /// <summary>The link survives exactly one redirect after issuance.</summary>
    [TempData] public string? IssuedLink { get; set; }
    /// <summary>Recipient name, retained after validation.</summary>
    [BindProperty] public string Name { get; set; } = "";
    /// <summary>Optional recipient address.</summary>
    [BindProperty] public string? Email { get; set; }
    /// <summary>The requested review deadline.</summary>
    [BindProperty] public DateTime? DueDate { get; set; }
    /// <summary>Explicit sharing scope; excerpts are the safe default.</summary>
    [BindProperty] public string Visibility { get; set; } = "AssignedSectionsOnly";
    /// <summary>An answer survives an invalid or stale POST.</summary>
    [BindProperty] public string? Answer { get; set; }
    /// <summary>Current source text and version for deliberate suggestion acceptance.</summary>
    public Dictionary<string, StoredSectionText?> Sources { get; } = [];

    /// <summary>Renders only sanitized Markdown.</summary>
    public string Render(string markdown) => renderer.Render(markdown);

    /// <summary>Opens a collection or an existing assignment.</summary>
    public async Task<IActionResult> OnGetAsync(string documentId, string reviewId, CancellationToken ct) =>
        await LoadAsync(documentId, reviewId, ct) ? Page() : NotFound();

    /// <summary>Issues the assignment and returns its shareable link.</summary>
    public async Task<IActionResult> OnPostIssueAsync(string documentId, string reviewId, string etag, CancellationToken ct)
    {
        if (!await LoadAsync(documentId, reviewId, ct)) { return NotFound(); }
        if (!ModelState.IsValid || DueDate is null)
        {
            Message = "Bitte einen Namen und eine gültige Frist angeben.";
            return Page();
        }
        var due = new DateTimeOffset(DateTime.SpecifyKind(DueDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));
        var result = await reviews.IssueAsync(new DocumentIdentifier(documentId), reviewId, etag, Name, Email, due, ct, Visibility);
        if (result.Error is not null) { Message = result.Error; return Page(); }
        IssuedLink = Url.Page("/Review/Index", null, new { documentId, reviewId }, Request.Scheme) + $"#token={result.Token}";
        return Redirect($"/dokumente/{documentId}/reviews/{reviewId}");
    }

    /// <summary>Removes a quotation from the collection.</summary>
    public async Task<IActionResult> OnPostRemoveAsync(string documentId, string reviewId, string passageId, string etag, CancellationToken ct)
    {
        if (!await LoadAsync(documentId, reviewId, ct)) { return NotFound(); }
        var result = await reviews.RemoveAsync(new DocumentIdentifier(documentId), reviewId, passageId, etag, ct);
        if (result.Error is not null) { Message = result.Error; return Page(); }
        return Redirect($"/dokumente/{documentId}/reviews/{reviewId}");
    }

    /// <summary>Resolves one response, accepts a review or withdraws its link.</summary>
    public async Task<IActionResult> OnPostDecideAsync(string documentId, string reviewId, string etag, string action, string? passageId, CancellationToken ct, string? textETag = null)
    {
        if (!await LoadAsync(documentId, reviewId, ct)) { return NotFound(); }
        var id = new DocumentIdentifier(documentId);
        var result = action switch
        {
            "apply" => await reviews.ApplySuggestionAsync(id, reviewId, etag, passageId ?? "", textETag ?? "", ct),
            "answer" or "reject" => await reviews.RespondAsync(id, reviewId, etag, passageId ?? "", action, Answer, ct),
            _ => await reviews.DecideAsync(id, reviewId, etag, action, passageId, ct)
        };
        if (result.Error is not null) { await LoadAsync(documentId, reviewId, ct); Message = result.Error; Response.StatusCode = 409; return Page(); }
        return Redirect($"/dokumente/{documentId}/reviews/{reviewId}");
    }

    private async Task<bool> LoadAsync(string documentId, string reviewId, CancellationToken ct)
    {
        try
        {
            var loaded = await reviews.LoadAsync(new DocumentIdentifier(documentId), reviewId, ct);
            if (loaded is null) { return false; }
            Stored = loaded;
            var document = await documents.ReadAsync(new DocumentIdentifier(documentId), ct);
            Sources.Clear();
            foreach (var passage in loaded.Review.Passages)
            {
                var section = new SectionIdentifier(passage.SectionId);
                Sources[passage.Id] = document?.Document.FindSection(section) is null ? null :
                    await documents.ReadSectionTextAsync(new DocumentIdentifier(documentId), section, ct);
            }
            return true;
        }
        catch (ArgumentException) { return false; }
    }
}
