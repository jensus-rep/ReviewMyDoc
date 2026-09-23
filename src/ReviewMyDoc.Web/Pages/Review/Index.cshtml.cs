// A review capability is redeemed by POST, never in a logged request URL.
// Each subsequent read or write rechecks the assignment's expiry and revocation.
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Markdown;
using ReviewMyDoc.Core.Reviews;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Web.Pages.Review;

/// <summary>The recipient's view, restricted to explicitly assigned passages.</summary>
[AllowAnonymous]
public sealed class IndexModel(ReviewService reviews, IDataProtectionProvider protection, IMarkdownRenderer renderer,
    [FromKeyedServices(ReviewLinkRateLimit.Name)] PartitionedRateLimiter<HttpContext> limiter) : PageModel
{
    /// <summary>Only the authorized assignment, never the source document.</summary>
    public StoredReview? Stored { get; private set; }
    /// <summary>Access or validation message.</summary>
    public string? Message { get; private set; }
    /// <summary>Recipient responses keyed by passage, retained on validation failures.</summary>
    [BindProperty(Name = "Feedback")] public Dictionary<string, string> Feedback { get; set; } = [];
    /// <summary>Feedback kinds selected for assigned passages only.</summary>
    [BindProperty(Name = "Kinds")] public Dictionary<string, string> Kinds { get; set; } = [];
    /// <summary>Suggested replacement text, preserved on validation errors.</summary>
    [BindProperty(Name = "Proposals")] public Dictionary<string, string> Proposals { get; set; } = [];
    /// <summary>The authorized frozen document, absent for restricted reviews.</summary>
    public DocumentVersion? ContextDocument { get; private set; }
    /// <summary>Change indicators reveal no current private source text.</summary>
    public IReadOnlyList<string> ChangedPassageIds { get; private set; } = [];

    /// <summary>Renders a quotation safely.</summary>
    public string Render(string markdown) => renderer.Render(markdown);

    /// <summary>Reads a review only after authenticating the scoped session.</summary>
    public async Task<IActionResult> OnGetAsync(string documentId, string reviewId, CancellationToken ct)
    {
        Stored = await SessionAsync(documentId, reviewId, ct);
        if (Stored is not null) { await LoadContextAsync(documentId, reviewId, ct); }
        else if (Request.Cookies.ContainsKey("reviewmydoc.review")) { return Denied(); }
        return Page();
    }

    /// <summary>Exchanges a raw capability for an encrypted, scoped session cookie.</summary>
    public async Task<IActionResult> OnPostOpenAsync(string documentId, string reviewId, string? token, CancellationToken ct)
    {
        using var lease = await limiter.AcquireAsync(HttpContext, cancellationToken: ct);
        if (!lease.IsAcquired)
        {
            RateLimitRefusal.Answer(Response, lease, TimeSpan.FromMinutes(1));
            Message = "Zu viele Versuche. Bitte in einer Minute erneut versuchen.";
            return Page();
        }
        if (token is null || token.Length != 64 || token.Any(c => !char.IsAsciiHexDigit(c))) { return Denied(); }
        try
        {
            var hash = ReviewService.Hash(token);
            var stored = await reviews.AccessAsync(new DocumentIdentifier(documentId), reviewId, hash, ct);
            if (stored is null) { return Denied(); }
            Response.Cookies.Append("reviewmydoc.review", Protector(documentId, reviewId).Protect(hash), new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = $"/review/{documentId}/{reviewId}",
                Expires = stored.Review.TokenExpiresAt,
            });
            return Redirect($"/review/{documentId}/{reviewId}");
        }
        catch (ArgumentException) { return Denied(); }
    }

    /// <summary>Returns the recipient's comments on assigned passages only.</summary>
    public async Task<IActionResult> OnPostReturnAsync(string documentId, string reviewId, string etag, CancellationToken ct)
    {
        Stored = await SessionAsync(documentId, reviewId, ct);
        if (Stored is null) { return Denied(); }
        await LoadContextAsync(documentId, reviewId, ct);
        if (Stored is null) { return Denied(); }
        if (!ModelState.IsValid) { Message = "Bitte die Eingaben prüfen."; return Page(); }
        var keys = Feedback.Keys.Concat(Kinds.Keys).Concat(Proposals.Keys).Distinct();
        var input = keys.ToDictionary(key => key, key => new ReviewFeedback(Feedback.GetValueOrDefault(key) ?? "",
            Kinds.GetValueOrDefault(key) ?? "Comment", Proposals.GetValueOrDefault(key)));
        var result = await reviews.ReturnFeedbackAsync(new DocumentIdentifier(documentId), reviewId, Stored.Review.TokenHash!, etag, input, ct);
        if (result.Error is not null) { Message = result.Error; Response.StatusCode = 409; return Page(); }
        return Redirect($"/review/{documentId}/{reviewId}");
    }

    private IDataProtector Protector(string documentId, string reviewId) => protection.CreateProtector("ReviewSession", documentId, reviewId);

    private async Task LoadContextAsync(string documentId, string reviewId, CancellationToken ct)
    {
        var reading = await reviews.ReadForReviewerAsync(new DocumentIdentifier(documentId), reviewId, Stored!.Review.TokenHash!, ct);
        Stored = reading?.Stored;
        ContextDocument = reading?.Document;
        ChangedPassageIds = reading?.ChangedPassageIds ?? [];
    }

    private async Task<StoredReview?> SessionAsync(string documentId, string reviewId, CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue("reviewmydoc.review", out var cookie)) { return null; }
        try { return await reviews.AccessAsync(new DocumentIdentifier(documentId), reviewId, Protector(documentId, reviewId).Unprotect(cookie), ct); }
        catch (CryptographicException) { return null; }
        catch (ArgumentException) { return null; }
    }

    private PageResult Denied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Stored = null;
        ContextDocument = null;
        Message = "Dieser Reviewlink ist ungültig, abgelaufen, widerrufen oder bereits abgeschlossen.";
        return Page();
    }
}
