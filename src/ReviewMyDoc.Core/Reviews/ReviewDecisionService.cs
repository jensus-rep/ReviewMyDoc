// Owns final feedback decisions and recoverable suggestion application.
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using static ReviewMyDoc.Core.Reviews.ReviewService;

namespace ReviewMyDoc.Core.Reviews;

/// <summary>Owner decisions, separated from reviewer capability access.</summary>
public sealed class ReviewDecisionService(ReviewStore reviews, IDocumentStore documents, DocumentEditingService editing, TimeProvider clock)
{
    /// <summary>Resolves feedback, accepts a completed review, or revokes its capability.</summary>
    public async Task<ReviewResult> DecideAsync(DocumentIdentifier documentId, string reviewId, string etag,
        string action, string? passageId, CancellationToken ct)
    {
        var stored = await reviews.ReadAsync(documentId, reviewId, ct);
        if (stored is null || stored.ETag.Value != etag) { return new(Error: Conflict); }
        var review = stored.Review;
        ReviewAssignment? changed = action switch
        {
            "resolve" when review.State == "Returned" && review.Passages.Any(p => p.Id == passageId && IsOpen(p) && p.FeedbackKind == "Comment") =>
                review with { Passages = [.. review.Passages.Select(p => p.Id == passageId ? p with { Resolved = true, Decision = "Resolved", DecidedAt = clock.GetUtcNow() } : p)] },
            "accept" when review.State == "Returned" && review.Passages.All(p => !IsOpen(p)) =>
                review with { State = "Accepted", TokenHash = null, AcceptedAt = clock.GetUtcNow() },
            "revoke" when review.State is "Draft" or "Sent" or "Returned" && review.Passages.All(p => p.Decision != "Applying") => review with { State = "Revoked", TokenHash = null },
            _ => null,
        };
        return changed is null ? new(Error: "Bitte zuerst alle Rückmeldungen bearbeiten.") : await WriteAsync(changed, stored, ct);
    }

    /// <summary>Answers questions or rejects suggestions exactly once.</summary>
    public async Task<ReviewResult> RespondAsync(DocumentIdentifier documentId, string reviewId, string etag,
        string passageId, string action, string? answer, CancellationToken ct)
    {
        var stored = await reviews.ReadAsync(documentId, reviewId, ct);
        var passage = stored?.Review.Passages.FirstOrDefault(p => p.Id == passageId);
        if (stored is null || stored.ETag.Value != etag || stored.Review.State != "Returned" || passage is null ||
            !IsOpen(passage) || passage.Decision == "Applying") { return new(Error: Conflict); }
        if (action == "answer" && (passage.FeedbackKind != "Question" || string.IsNullOrWhiteSpace(answer) || answer.Length > 10_000))
        { return new(Error: "Bitte die Rückfrage mit höchstens 10.000 Zeichen beantworten."); }
        if (action != "answer" && (action != "reject" || passage.FeedbackKind != "Suggestion")) { return new(Error: Conflict); }
        var changed = passage with
        {
            Resolved = true,
            Decision = action == "answer" ? "Answered" : "Rejected",
            Answer = action == "answer" ? answer!.Trim() : null,
            DecidedAt = clock.GetUtcNow()
        };
        return await WriteAsync(stored.Review with { Passages = [.. stored.Review.Passages.Select(p => p.Id == passageId ? changed : p)] }, stored, ct);
    }

    /// <summary>Applies an unambiguous quotation replacement with recoverable two-entry persistence.</summary>
    public async Task<ReviewResult> ApplySuggestionAsync(DocumentIdentifier documentId, string reviewId, string etag,
        string passageId, string textETag, CancellationToken ct)
    {
        var stored = await reviews.ReadAsync(documentId, reviewId, ct);
        var passage = stored?.Review.Passages.FirstOrDefault(p => p.Id == passageId);
        if (stored is null || stored.ETag.Value != etag || stored.Review.State != "Returned" || passage is null ||
            !IsOpen(passage) || passage.FeedbackKind != "Suggestion") { return new(Error: Conflict); }
        var sectionId = new SectionIdentifier(passage.SectionId);
        var current = await documents.ReadSectionTextAsync(documentId, sectionId, ct);
        if (current is null) { return new(Error: "Der Quellabschnitt wurde entfernt. Der Vorschlag kann nur abgelehnt werden."); }
        if (passage.Decision != "Applying")
        {
            if (current.ETag.Value != textETag) { return new(Error: Conflict); }
            var at = current.Content.IndexOf(passage.Markdown, StringComparison.Ordinal);
            if (at < 0 || current.Content.IndexOf(passage.Markdown, at + 1, StringComparison.Ordinal) >= 0)
            { return new(Error: "Die Passage lässt sich im aktuellen Text nicht eindeutig zuordnen. Bitte den Text manuell bearbeiten und den Vorschlag ablehnen."); }
            var replacement = current.Content[..at] + passage.ProposedMarkdown + current.Content[(at + passage.Markdown.Length)..];
            if (replacement.Length > 500_000) { return new(Error: "Der Abschnitt würde zu lang werden."); }
            passage = passage with
            {
                Decision = "Applying",
                ApplicationText = replacement,
                ApplicationETag = current.ETag.Value,
                ApplicationHash = Hash(replacement)
            };
            var reserved = await WriteAsync(stored.Review with { Passages = [.. stored.Review.Passages.Select(p => p.Id == passageId ? passage : p)] }, stored, ct);
            if (reserved.Error is not null) { return reserved; }
            stored = reserved.Stored!;
        }
        // An interrupted response may be retried without replacing the passage twice.
        if (Hash(current.Content) != passage.ApplicationHash)
        {
            var saved = await editing.SaveAsync(documentId, sectionId, passage.ApplicationText!, new ETag(passage.ApplicationETag!), ct);
            if (saved is not ObjectWriteResult.Written)
            {
                var reset = passage with { Decision = null, ApplicationText = null, ApplicationETag = null, ApplicationHash = null };
                await WriteAsync(stored.Review with { Passages = [.. stored.Review.Passages.Select(p => p.Id == passageId ? reset : p)] }, stored, ct);
                return new(Error: Conflict);
            }
        }
        var complete = passage with
        {
            Resolved = true,
            Decision = "Applied",
            DecidedAt = clock.GetUtcNow(),
            ApplicationText = null,
            ApplicationETag = null,
            ApplicationHash = null
        };
        return await WriteAsync(stored.Review with { Passages = [.. stored.Review.Passages.Select(p => p.Id == passageId ? complete : p)] }, stored, ct);
    }

    private async Task<ReviewResult> WriteAsync(ReviewAssignment review, StoredReview? before, CancellationToken ct)
    {
        var written = await reviews.WriteAsync(review, before is null ? WriteCondition.MustNotExist : WriteCondition.MustMatch(before.ETag), ct);
        return written is ObjectWriteResult.Written success ? new(new StoredReview(review, success.ETag)) : new(Error: Conflict);
    }

}
