// Owns named collection creation and completion independently of invitation and feedback.
// Closing a collection preserves its passages and its ability to be assigned later.
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Reviews;

/// <summary>Creates and closes the owner's named review sets.</summary>
public sealed class ReviewCollectionService(ReviewStore reviews, IDocumentStore documents, TimeProvider clock)
{
    /// <summary>Persists an empty named set before the first passage is selected.</summary>
    public async Task<ReviewResult> CreateAsync(DocumentIdentifier documentId, string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80)
        { return new(Error: "Bitte einen Namen mit höchstens 80 Zeichen eingeben."); }
        var document = await documents.ReadAsync(documentId, ct);
        if (document is null || document.Document.State == DocumentState.Approved) { return new(Error: ReviewService.Conflict); }
        var review = new ReviewAssignment(DocumentIdentifier.Draw().Value, documentId.Value, document.Document.Title,
            "Draft", [], clock.GetUtcNow(), SetName: name.Trim());
        return await WriteAsync(review, WriteCondition.MustNotExist, ct);
    }

    /// <summary>Closes or reopens collection without issuing or accepting the review.</summary>
    public async Task<ReviewResult> SetClosedAsync(DocumentIdentifier documentId, string reviewId, string etag, bool closed, CancellationToken ct)
    {
        var document = await documents.ReadAsync(documentId, ct);
        var stored = await reviews.ReadAsync(documentId, reviewId, ct);
        if (document is null || document.Document.State == DocumentState.Approved || stored is null ||
            stored.ETag.Value != etag || stored.Review.State != "Draft") { return new(Error: ReviewService.Conflict); }
        return await WriteAsync(stored.Review with { CollectionClosedAt = closed ? clock.GetUtcNow() : null },
            WriteCondition.MustMatch(stored.ETag), ct);
    }

    private async Task<ReviewResult> WriteAsync(ReviewAssignment review, WriteCondition condition, CancellationToken ct)
    {
        var result = await reviews.WriteAsync(review, condition, ct);
        return result is ObjectWriteResult.Written written ? new(new StoredReview(review, written.ETag)) : new(Error: ReviewService.Conflict);
    }
}
