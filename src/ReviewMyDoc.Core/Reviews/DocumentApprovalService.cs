// Owns document release independently of invitation or feedback presentation.
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using static ReviewMyDoc.Core.Reviews.ReviewService;

namespace ReviewMyDoc.Core.Reviews;

/// <summary>Validates all assignments before releasing a document.</summary>
public sealed class DocumentApprovalService(ReviewStore reviews, IDocumentStore documents, TimeProvider clock)
{
    /// <summary>Releases a document only when every persisted assignment is closed.</summary>
    public async Task<string?> SetDocumentApprovalAsync(DocumentIdentifier documentId, string etag, bool approve, CancellationToken ct)
    {
        var stored = await documents.ReadAsync(documentId, ct);
        if (stored is null || stored.ETag.Value != etag) { return Conflict; }
        if (approve)
        {
            var assignments = await reviews.ListAsync(documentId, ct);
            if (!assignments.Any(r => r.Review.State == "Accepted")) { return "Vor der Freigabe muss mindestens ein Review abgenommen sein."; }
            if (assignments.Any(r => r.Review.State is not ("Accepted" or "Revoked")))
            { return "Bitte zuerst alle Sammlungen zuweisen und alle Aufträge abnehmen oder widerrufen."; }
        }
        else if (stored.Document.State != DocumentState.Approved) { return Conflict; }
        var result = await documents.WriteAsync(stored.Document.WithState(approve ? DocumentState.Approved : DocumentState.Draft, clock.GetUtcNow()),
            WriteCondition.MustMatch(stored.ETag), ct);
        return result is ObjectWriteResult.Written ? null : Conflict;
    }
}
