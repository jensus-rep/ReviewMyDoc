// Exercises all pipeline stages, clock-dependent deadlines and closed-work retention.
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Reviews;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Projection contracts independent of web presentation.</summary>
public sealed class ReviewPipelineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public async Task Four_stages_empty_states_deadlines_and_retention_are_consistent()
    {
        using var app = new OwnerApplication();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var reviews = app.Services.GetRequiredService<ReviewStore>();
        var clock = new FixedClock();
        var pipeline = new ReviewPipeline(store, reviews, clock);
        Assert.Empty(await pipeline.LoadAsync(Ct));
        using var owner = await app.CreateOwnerClientAsync();
        var empty = await owner.GetStringAsync("/", Ct);
        foreach (var label in new[] { "Draußen", "Zurück", "Bei dir", "Erledigt" }) { Assert.Contains(label, empty); }
        var doc = Assert.IsType<DocumentResult.Success>(await app.Services.GetRequiredService<DocumentService>().CreateDocumentAsync("owner", "Pipeline", Ct));
        var now = clock.GetUtcNow();
        var comment = new ReviewPassage("p1", "s1", "Heading", "Text", "hash", "Comment");
        var entries = new[]
        {
            new ReviewAssignment("r1", doc.Document.Id.Value, "Overdue", "Sent", [], now, DueAt: now.AddDays(-1)),
            new ReviewAssignment("r2", doc.Document.Id.Value, "Future", "Sent", [], now, DueAt: now.AddDays(2)),
            new ReviewAssignment("r3", doc.Document.Id.Value, "Returned", "Returned", [comment], now),
            new ReviewAssignment("r4", doc.Document.Id.Value, "Question", "Returned", [comment with { FeedbackKind = "Question" }], now),
            new ReviewAssignment("r5", doc.Document.Id.Value, "Ready", "Returned", [], now),
            new ReviewAssignment("r6", doc.Document.Id.Value, "Accepted", "Accepted", [], now, AcceptedAt: now),
            new ReviewAssignment("r7", doc.Document.Id.Value, "Old", "Accepted", [], now.AddDays(-40), AcceptedAt: now.AddDays(-31)),
            new ReviewAssignment("r8", doc.Document.Id.Value, "Revoked", "Revoked", [], now)
        };
        foreach (var entry in entries) { await reviews.WriteAsync(entry, WriteCondition.MustNotExist, Ct); }
        var result = await pipeline.LoadAsync(Ct);
        Assert.Equal(6, result.Count);
        Assert.Equal(6, result.Select(r => r.Id).Distinct().Count());
        Assert.Equal(new[] { "r1", "r2" }, result.Where(r => ReviewPipeline.Stage(r) == "Draußen").Select(r => r.Id));
        Assert.Single(result, r => ReviewPipeline.Stage(r) == "Zurück");
        Assert.Equal(2, result.Count(r => ReviewPipeline.Stage(r) == "Bei dir"));
        Assert.Single(result, r => ReviewPipeline.Stage(r) == "Erledigt");
        Assert.True(pipeline.IsOverdue(entries[0]));
        Assert.False(pipeline.IsOverdue(entries[1]));
        Assert.Equal("Noch 2 Tage", pipeline.Remaining(entries[1]));
        var html = await owner.GetStringAsync("/", Ct);
        foreach (var title in new[] { "Overdue", "Future", "Returned", "Question", "Ready", "Accepted" }) { Assert.Contains(title, html); }
        Assert.DoesNotContain(">Revoked<", html);
    }
}
