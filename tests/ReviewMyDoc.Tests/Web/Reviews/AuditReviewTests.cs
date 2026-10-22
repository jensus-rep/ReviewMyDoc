// Checks the audit trail through real review operations, including retries,
// feedback decisions and the boundary between reviewer and owner histories.
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Audit;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Reviews;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Every review mutation records only safe, committed facts.</summary>
public sealed class AuditReviewTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(AuditEventKind.ReviewSetCreated)]
    [InlineData(AuditEventKind.CollectionClosed)]
    [InlineData(AuditEventKind.CollectionReopened)]
    [InlineData(AuditEventKind.PassageCollected)]
    [InlineData(AuditEventKind.PassageRemoved)]
    [InlineData(AuditEventKind.ReviewIssued)]
    [InlineData(AuditEventKind.ReviewReturned)]
    [InlineData(AuditEventKind.FeedbackResolved)]
    [InlineData(AuditEventKind.QuestionAnswered)]
    [InlineData(AuditEventKind.SuggestionRejected)]
    [InlineData(AuditEventKind.SuggestionApplied)]
    [InlineData(AuditEventKind.ReviewAccepted)]
    [InlineData(AuditEventKind.ReviewRevoked)]
    [InlineData(AuditEventKind.DocumentApproved)]
    [InlineData(AuditEventKind.DocumentReopened)]
    public async Task Each_review_change_appends_one_safe_event_and_rejects_stale_retries(AuditEventKind kind)
    {
        using var app = new OwnerApplication();
        var documents = app.Services.GetRequiredService<DocumentService>();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var service = app.Services.GetRequiredService<ReviewService>();
        var audit = app.Services.GetRequiredService<AuditLog>();
        var objects = app.Services.GetRequiredService<IObjectStore>();
        var created = Assert.IsType<DocumentResult.Success>(await documents.CreateDocumentAsync("owner", "Private document", Ct));
        var id = created.Document.Id;
        var added = Assert.IsType<DocumentResult.Success>(await documents.AddSectionAsync(id, "Private heading", created.ETag, Ct));
        var section = added.Document.Sections[0].Id;
        var blank = (await store.ReadSectionTextAsync(id, section, Ct))!;
        var saved = Assert.IsType<ObjectWriteResult.Written>(await app.Services.GetRequiredService<DocumentEditingService>()
            .SaveAsync(id, section, "PRIVATE REVIEW CONTENT", blank.ETag, Ct));
        var collected = await service.CollectAsync(id, section, "PRIVATE REVIEW CONTENT", saved.ETag.Value, null, null, Ct);
        var review = collected.Stored!;
        var passageId = review.Review.Passages[0].Id;
        string? token = null;

        if (kind == AuditEventKind.CollectionReopened)
        {
            review = (await service.SetCollectionClosedAsync(id, review.Review.Id, review.ETag.Value, true, Ct)).Stored!;
        }
        var needsIssue = kind is AuditEventKind.ReviewReturned or AuditEventKind.FeedbackResolved or AuditEventKind.QuestionAnswered
            or AuditEventKind.SuggestionRejected or AuditEventKind.SuggestionApplied or AuditEventKind.ReviewAccepted
            or AuditEventKind.DocumentApproved or AuditEventKind.DocumentReopened;
        if (needsIssue)
        {
            var issued = await service.IssueAsync(id, review.Review.Id, review.ETag.Value,
                "PRIVATE REVIEWER NAME", "private@example.org", DateTimeOffset.UtcNow.AddDays(7), Ct);
            Assert.Null(issued.Error);
            review = issued.Stored!;
            token = issued.Token!;
        }
        if (needsIssue && kind != AuditEventKind.ReviewReturned)
        {
            var input = kind switch
            {
                AuditEventKind.QuestionAnswered => new ReviewFeedback("PRIVATE QUESTION", "Question"),
                AuditEventKind.SuggestionRejected or AuditEventKind.SuggestionApplied => new ReviewFeedback("PRIVATE SUGGESTION", "Suggestion", "PRIVATE REPLACEMENT"),
                AuditEventKind.FeedbackResolved => new ReviewFeedback("PRIVATE COMMENT"),
                _ => new ReviewFeedback("")
            };
            review = (await service.ReturnFeedbackAsync(id, review.Review.Id, ReviewService.Hash(token!), review.ETag.Value,
                new Dictionary<string, ReviewFeedback> { [passageId] = input }, Ct)).Stored!;
        }
        if (kind is AuditEventKind.DocumentApproved or AuditEventKind.DocumentReopened)
        {
            review = (await service.DecideAsync(id, review.Review.Id, review.ETag.Value, "accept", null, Ct)).Stored!;
        }
        if (kind == AuditEventKind.DocumentReopened)
        {
            Assert.Null(await service.SetDocumentApprovalAsync(id, (await store.ReadAsync(id, Ct))!.ETag.Value, true, Ct));
        }
        var document = (await store.ReadAsync(id, Ct))!;
        var before = await RawAsync();
        var beforeEvents = await audit.ReadAsync(id, Ct);

        async Task<string?> ChangeAsync()
        {
            ReviewResult? result = null;
            switch (kind)
            {
                case AuditEventKind.ReviewSetCreated:
                    result = await service.CreateSetAsync(id, "PRIVATE SET", Ct); break;
                case AuditEventKind.CollectionClosed:
                case AuditEventKind.CollectionReopened:
                    result = await service.SetCollectionClosedAsync(id, review.Review.Id, review.ETag.Value, kind == AuditEventKind.CollectionClosed, Ct); break;
                case AuditEventKind.PassageCollected:
                    result = await service.CollectAsync(id, section, "PRIVATE REVIEW CONTENT", saved.ETag.Value, review.Review.Id, review.ETag.Value, Ct); break;
                case AuditEventKind.PassageRemoved:
                    result = await service.RemoveAsync(id, review.Review.Id, passageId, review.ETag.Value, Ct); break;
                case AuditEventKind.ReviewIssued:
                    result = await service.IssueAsync(id, review.Review.Id, review.ETag.Value, "PRIVATE REVIEWER NAME", "private@example.org", DateTimeOffset.UtcNow.AddDays(7), Ct); break;
                case AuditEventKind.ReviewReturned:
                    result = await service.ReturnAsync(id, review.Review.Id, ReviewService.Hash(token!), review.ETag.Value,
                        new Dictionary<string, string> { [passageId] = "PRIVATE COMMENT" }, Ct); break;
                case AuditEventKind.FeedbackResolved:
                case AuditEventKind.ReviewAccepted:
                case AuditEventKind.ReviewRevoked:
                    var action = kind == AuditEventKind.FeedbackResolved ? "resolve" : kind == AuditEventKind.ReviewAccepted ? "accept" : "revoke";
                    result = await service.DecideAsync(id, review.Review.Id, review.ETag.Value, action,
                        kind == AuditEventKind.FeedbackResolved ? passageId : null, Ct); break;
                case AuditEventKind.QuestionAnswered:
                case AuditEventKind.SuggestionRejected:
                    result = await service.RespondAsync(id, review.Review.Id, review.ETag.Value, passageId,
                        kind == AuditEventKind.QuestionAnswered ? "answer" : "reject", "PRIVATE ANSWER", Ct); break;
                case AuditEventKind.SuggestionApplied:
                    result = await service.ApplySuggestionAsync(id, review.Review.Id, review.ETag.Value, passageId, saved.ETag.Value, Ct); break;
                case AuditEventKind.DocumentApproved:
                case AuditEventKind.DocumentReopened:
                    return await service.SetDocumentApprovalAsync(id, document.ETag.Value, kind == AuditEventKind.DocumentApproved, Ct);
            }
            if (result is not null)
            {
                if (result.Token is not null) { token = result.Token; }
            }
            return result?.Error;
        }
        Assert.Null(await ChangeAsync());
        var after = await audit.ReadAsync(id, Ct);
        var addedEvents = after.Take(after.Count - beforeEvents.Count).ToArray();
        var entry = Assert.Single(addedEvents, e => e.Kind == kind);
        Assert.Equal(kind is AuditEventKind.ReviewIssued or AuditEventKind.SuggestionApplied ? 2 : 1, addedEvents.Length);
        Assert.Equal(kind == AuditEventKind.ReviewReturned ? AuditActor.Reviewer : AuditActor.Owner, entry.Actor);
        Assert.Equal(kind == AuditEventKind.ReviewReturned ? review.Review.Id : "owner", entry.ActorId);
        var raw = await RawAsync();
        Assert.StartsWith(before, raw);
        Assert.DoesNotContain("PRIVATE", raw);
        Assert.DoesNotContain("private@example.org", raw);
        Assert.DoesNotContain(review.Review.Passages[0].SourceHash, raw);
        if (token is not null)
        {
            Assert.DoesNotContain(token, raw);
            Assert.DoesNotContain(ReviewService.Hash(token), raw);
        }
        if (kind != AuditEventKind.ReviewSetCreated)
        {
            // Reuse the request's original ETag, as a browser retry does.
            Assert.NotNull(await ChangeAsync());
            Assert.Equal(raw, await RawAsync());
        }

        async Task<string> RawAsync() => Assert.IsType<ObjectReadResult.Found>(
            await objects.ReadAsync($"documents/{id}/audit.log", Ct)).Content;
    }
}
