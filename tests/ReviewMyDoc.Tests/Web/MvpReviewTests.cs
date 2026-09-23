// Verifies the MVP's security, feedback decisions and ordinary HTML workflows
// against real persistence and the application's HTTP boundary.
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Reviews;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Contracts for the complete non-AI review workflow.</summary>
public sealed class MvpReviewTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("AssignedSectionsOnly", false)]
    [InlineData("WholeDocument", true)]
    public async Task Visibility_is_server_enforced_and_always_frozen(string visibility, bool full)
    {
        using var app = new OwnerApplication();
        var (id, section, review, token) = await IssuedAsync(app, visibility);
        var service = app.Services.GetRequiredService<ReviewService>();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var current = (await store.ReadSectionTextAsync(id, section, Ct))!;
        await app.Services.GetRequiredService<DocumentEditingService>().SaveAsync(id, section, "NEW PRIVATE VERSION", current.ETag, Ct);
        using var client = app.CreateAnonymousClient();
        var path = $"/review/{id}/{review.Review.Id}";
        var html = await OpenAsync(client, path, token);
        Assert.Equal(full, html.Contains("SECRET CONTEXT", StringComparison.Ordinal));
        Assert.DoesNotContain("NEW PRIVATE VERSION", html);
        Assert.Contains("Chosen passage", html);
        var forbidden = await service.ReturnFeedbackAsync(id, review.Review.Id, ReviewService.Hash(token), review.ETag.Value,
            new Dictionary<string, ReviewFeedback> { [section.Value] = new("not assigned") }, Ct);
        Assert.NotNull(forbidden.Error);
        var objects = app.Services.GetRequiredService<IObjectStore>();
        var persisted = Assert.IsType<ObjectReadResult.Found>(await objects.ReadAsync($"documents/{id}/reviews/{review.Review.Id}.json", Ct));
        Assert.DoesNotContain(token, persisted.Content);
        Assert.DoesNotContain(token, string.Join("\n", app.LogMessages));
    }

    [Fact]
    public async Task Question_answer_is_visible_until_acceptance_then_cookie_and_token_are_denied()
    {
        using var app = new OwnerApplication();
        var (id, _, issued, token) = await IssuedAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        var passage = issued.Review.Passages[0];
        using var reviewer = app.CreateAnonymousClient();
        var path = $"/review/{id}/{issued.Review.Id}";
        var html = await OpenAsync(reviewer, path, token);
        var returned = await reviewer.PostAsync(path + "?handler=Return", Form(html,
            ("etag", issued.ETag.Value), ($"Feedback[{passage.Id}]", "Warum?"), ($"Kinds[{passage.Id}]", "Question")), Ct);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var current = (await service.LoadAsync(id, issued.Review.Id, Ct))!;
        Assert.Equal("Bei dir", ReviewPipeline.Stage(current.Review));
        Assert.NotNull((await service.DecideAsync(id, current.Review.Id, current.ETag.Value, "accept", null, Ct)).Error);
        Assert.NotNull((await service.DecideAsync(id, current.Review.Id, current.ETag.Value, "resolve", passage.Id, Ct)).Error);
        var answered = await service.RespondAsync(id, current.Review.Id, current.ETag.Value, passage.Id, "answer", "Weil es hilft.", Ct);
        Assert.Null(answered.Error);
        Assert.Contains("Weil es hilft.", await reviewer.GetStringAsync(path, Ct));
        Assert.NotNull((await service.RespondAsync(id, current.Review.Id, answered.Stored!.ETag.Value, passage.Id, "answer", "Andere Antwort", Ct)).Error);
        var accepted = await service.DecideAsync(id, current.Review.Id, answered.Stored!.ETag.Value, "accept", null, Ct);
        Assert.Null(accepted.Error);
        Assert.Null(await service.AccessAsync(id, current.Review.Id, ReviewService.Hash(token), Ct));
        var denied = await reviewer.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.DoesNotContain("Chosen passage", await denied.Content.ReadAsStringAsync(Ct));
        var latePost = await reviewer.PostAsync(path + "?handler=Return", Form(html, ("etag", accepted.Stored!.ETag.Value)), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, latePost.StatusCode);
        using var fresh = app.CreateAnonymousClient();
        var invitation = await fresh.GetStringAsync(path, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await fresh.PostAsync(path + "?handler=Open", Form(invitation, ("token", token)), Ct)).StatusCode);
    }

    [Fact]
    public async Task Suggestion_requires_current_text_etag_and_is_applied_only_once()
    {
        using var app = new OwnerApplication();
        var (id, section, issued, token) = await IssuedAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var p = issued.Review.Passages[0];
        var returned = await service.ReturnFeedbackAsync(id, issued.Review.Id, ReviewService.Hash(token), issued.ETag.Value,
            new Dictionary<string, ReviewFeedback> { [p.Id] = new("Klarer formulieren", "Suggestion", "Better passage") }, Ct);
        var original = (await store.ReadSectionTextAsync(id, section, Ct))!;
        await app.Services.GetRequiredService<DocumentEditingService>().SaveAsync(id, section, original.Content + "\nAnother edit", original.ETag, Ct);
        Assert.NotNull((await service.ApplySuggestionAsync(id, issued.Review.Id, returned.Stored!.ETag.Value, p.Id, original.ETag.Value, Ct)).Error);
        var current = (await store.ReadSectionTextAsync(id, section, Ct))!;
        var applied = await service.ApplySuggestionAsync(id, issued.Review.Id, returned.Stored.ETag.Value, p.Id, current.ETag.Value, Ct);
        Assert.Null(applied.Error);
        var text = (await store.ReadSectionTextAsync(id, section, Ct))!;
        Assert.Equal("Better passage\n\nSECRET CONTEXT\nAnother edit", text.Content);
        Assert.NotNull((await service.ApplySuggestionAsync(id, issued.Review.Id, applied.Stored!.ETag.Value, p.Id, text.ETag.Value, Ct)).Error);
        Assert.Equal(text, await store.ReadSectionTextAsync(id, section, Ct));
    }

    [Fact]
    public async Task Ambiguous_suggestion_is_refused_and_rejection_preserves_text()
    {
        using var app = new OwnerApplication();
        var (id, section, issued, token) = await IssuedAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var p = issued.Review.Passages[0];
        var returned = await service.ReturnFeedbackAsync(id, issued.Review.Id, ReviewService.Hash(token), issued.ETag.Value,
            new Dictionary<string, ReviewFeedback> { [p.Id] = new("", "Suggestion", "Better") }, Ct);
        var original = (await store.ReadSectionTextAsync(id, section, Ct))!;
        await app.Services.GetRequiredService<DocumentEditingService>().SaveAsync(id, section, "Chosen passage and Chosen passage", original.ETag, Ct);
        var current = (await store.ReadSectionTextAsync(id, section, Ct))!;
        Assert.NotNull((await service.ApplySuggestionAsync(id, issued.Review.Id, returned.Stored!.ETag.Value, p.Id, current.ETag.Value, Ct)).Error);
        var rejected = await service.RespondAsync(id, issued.Review.Id, returned.Stored.ETag.Value, p.Id, "reject", null, Ct);
        Assert.Null(rejected.Error);
        Assert.Equal(current, await store.ReadSectionTextAsync(id, section, Ct));
        Assert.NotNull((await service.RespondAsync(id, issued.Review.Id, rejected.Stored!.ETag.Value, p.Id, "reject", null, Ct)).Error);
    }

    [Fact]
    public async Task Approval_requires_closed_reviews_and_explicit_reopening_before_editing()
    {
        using var app = new OwnerApplication();
        var (id, section, issued, token) = await IssuedAsync(app);
        var reviews = app.Services.GetRequiredService<ReviewService>();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var doc = (await store.ReadAsync(id, Ct))!;
        Assert.Equal(DocumentState.InReview, doc.Document.State);
        Assert.NotNull(await reviews.SetDocumentApprovalAsync(id, doc.ETag.Value, true, Ct));
        var returned = await reviews.ReturnAsync(id, issued.Review.Id, ReviewService.Hash(token), issued.ETag.Value, new Dictionary<string, string>(), Ct);
        var accepted = await reviews.DecideAsync(id, issued.Review.Id, returned.Stored!.ETag.Value, "accept", null, Ct);
        Assert.Null(accepted.Error);
        Assert.Null(await reviews.SetDocumentApprovalAsync(id, doc.ETag.Value, true, Ct));
        var approved = (await store.ReadAsync(id, Ct))!;
        Assert.Equal(DocumentState.Approved, approved.Document.State);
        var text = (await store.ReadSectionTextAsync(id, section, Ct))!;
        Assert.IsType<ObjectWriteResult.Conflict>(await app.Services.GetRequiredService<DocumentEditingService>().SaveAsync(id, section, "No", text.ETag, Ct));
        Assert.IsType<DocumentResult.Conflict>(await app.Services.GetRequiredService<DocumentService>().RenameDocumentAsync(id, "No", approved.ETag, Ct));
        Assert.NotNull(await reviews.SetDocumentApprovalAsync(id, doc.ETag.Value, false, Ct));
        Assert.Null(await reviews.SetDocumentApprovalAsync(id, approved.ETag.Value, false, Ct));
        Assert.IsType<ObjectWriteResult.Written>(await app.Services.GetRequiredService<DocumentEditingService>().SaveAsync(id, section, "Allowed", text.ETag, Ct));
    }

    [Fact]
    public async Task Html_fallback_preserves_conflicting_input_and_other_sections()
    {
        using var app = new OwnerApplication();
        using var owner = await app.CreateOwnerClientAsync();
        var (id, section, _, _) = await IssuedAsync(app);
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var doc = (await store.ReadAsync(id, Ct))!;
        var added = Assert.IsType<DocumentResult.Success>(await app.Services.GetRequiredService<DocumentService>().AddSectionAsync(id, "Other section", doc.ETag, Ct));
        var otherId = added.Document.Sections[1].Id;
        var other = (await store.ReadSectionTextAsync(id, otherId, Ct))!;
        var first = (await store.ReadSectionTextAsync(id, section, Ct))!;
        var html = await owner.GetStringAsync($"/dokumente/{id}", Ct);
        Assert.DoesNotMatch("data-editor-surface[^>]*hidden", html);
        Assert.Contains("CollectSection", html);
        await app.Services.GetRequiredService<DocumentEditingService>().SaveAsync(id, section, "Concurrent edit", first.ETag, Ct);
        var conflict = await owner.PostAsync($"/dokumente/{id}?handler=Save", Form(html, ("sectionId", section.Value),
            ("etag", first.ETag.Value), ("markdown", "My unsaved text")), Ct);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("text/html", conflict.Content.Headers.ContentType!.ToString());
        Assert.Contains("My unsaved text", await conflict.Content.ReadAsStringAsync(Ct));
        Assert.Equal(other, await store.ReadSectionTextAsync(id, otherId, Ct));
        Assert.Equal("Concurrent edit", (await store.ReadSectionTextAsync(id, section, Ct))!.Content);
    }

    [Fact]
    public async Task Interrupted_application_is_private_and_resumes_without_double_replacement()
    {
        using var app = new OwnerApplication();
        var (id, section, issued, token) = await IssuedAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var reviewStore = app.Services.GetRequiredService<ReviewStore>();
        var p = issued.Review.Passages[0];
        var returned = await service.ReturnFeedbackAsync(id, issued.Review.Id, ReviewService.Hash(token), issued.ETag.Value,
            new Dictionary<string, ReviewFeedback> { [p.Id] = new("", "Suggestion", "Chosen passage, clarified") }, Ct);
        var current = (await store.ReadSectionTextAsync(id, section, Ct))!;
        var target = "Chosen passage, clarified\n\nSECRET CONTEXT";
        var pending = returned.Stored!.Review with
        {
            Passages = [returned.Stored.Review.Passages[0] with
        { Decision = "Applying", ApplicationText = target, ApplicationETag = current.ETag.Value, ApplicationHash = ReviewService.Hash(target) }]
        };
        var reserved = Assert.IsType<ObjectWriteResult.Written>(await reviewStore.WriteAsync(pending, WriteCondition.MustMatch(returned.Stored.ETag), Ct));
        var exposed = await service.ReadForReviewerAsync(id, issued.Review.Id, ReviewService.Hash(token), Ct);
        Assert.Null(exposed!.Document);
        Assert.Null(exposed.Stored.Review.Passages[0].ApplicationText);
        Assert.NotNull((await service.DecideAsync(id, issued.Review.Id, reserved.ETag.Value, "accept", null, Ct)).Error);
        Assert.NotNull((await service.RespondAsync(id, issued.Review.Id, reserved.ETag.Value, p.Id, "reject", null, Ct)).Error);
        await app.Services.GetRequiredService<DocumentEditingService>().SaveAsync(id, section, target, current.ETag, Ct);
        var written = (await store.ReadSectionTextAsync(id, section, Ct))!;
        var resumed = await service.ApplySuggestionAsync(id, issued.Review.Id, reserved.ETag.Value, p.Id, current.ETag.Value, Ct);
        Assert.Null(resumed.Error);
        Assert.Equal("Applied", resumed.Stored!.Review.Passages[0].Decision);
        Assert.Equal(written, await store.ReadSectionTextAsync(id, section, Ct));
    }

    [Fact]
    public async Task Open_collection_prevents_release_and_invalid_feedback_leaves_the_assignment_sent()
    {
        using var app = new OwnerApplication();
        var (id, section, issued, token) = await IssuedAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var p = issued.Review.Passages[0];
        var invalid = await service.ReturnFeedbackAsync(id, issued.Review.Id, ReviewService.Hash(token), issued.ETag.Value,
            new Dictionary<string, ReviewFeedback> { [p.Id] = new("", "Question") }, Ct);
        Assert.NotNull(invalid.Error);
        Assert.Equal("Sent", (await service.LoadAsync(id, issued.Review.Id, Ct))!.Review.State);
        var returned = await service.ReturnAsync(id, issued.Review.Id, ReviewService.Hash(token), issued.ETag.Value, new Dictionary<string, string>(), Ct);
        await service.DecideAsync(id, issued.Review.Id, returned.Stored!.ETag.Value, "accept", null, Ct);
        var source = (await store.ReadSectionTextAsync(id, section, Ct))!;
        var draft = await service.CollectAsync(id, section, "Chosen passage", source.ETag.Value, null, null, Ct);
        var doc = (await store.ReadAsync(id, Ct))!;
        Assert.NotNull(await service.SetDocumentApprovalAsync(id, doc.ETag.Value, true, Ct));
        Assert.NotEqual(DocumentState.Approved, (await store.ReadAsync(id, Ct))!.Document.State);
        Assert.Null((await service.DecideAsync(id, draft.Stored!.Review.Id, draft.Stored.ETag.Value, "revoke", null, Ct)).Error);
        Assert.Null(await service.SetDocumentApprovalAsync(id, doc.ETag.Value, true, Ct));
    }

    private static async Task<(DocumentIdentifier, SectionIdentifier, StoredReview, string)> IssuedAsync(OwnerApplication app, string visibility = "AssignedSectionsOnly")
    {
        var documents = app.Services.GetRequiredService<DocumentService>();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var reviews = app.Services.GetRequiredService<ReviewService>();
        var doc = Assert.IsType<DocumentResult.Success>(await documents.CreateDocumentAsync("owner", "MVP document", Ct));
        var added = Assert.IsType<DocumentResult.Success>(await documents.AddSectionAsync(doc.Document.Id, "Text", doc.ETag, Ct));
        var section = added.Document.Sections[0].Id;
        var blank = (await store.ReadSectionTextAsync(doc.Document.Id, section, Ct))!;
        var saved = Assert.IsType<ObjectWriteResult.Written>(await app.Services.GetRequiredService<DocumentEditingService>().SaveAsync(doc.Document.Id, section,
            "Chosen passage\n\nSECRET CONTEXT", blank.ETag, Ct));
        var draft = await reviews.CollectAsync(doc.Document.Id, section, "Chosen passage", saved.ETag.Value, null, null, Ct);
        var issued = await reviews.IssueAsync(doc.Document.Id, draft.Stored!.Review.Id, draft.Stored.ETag.Value, "Reviewer", null, DateTimeOffset.UtcNow.AddDays(7), Ct, visibility);
        Assert.Null(issued.Error);
        return (doc.Document.Id, section, issued.Stored!, issued.Token!);
    }

    private static FormUrlEncodedContent Form(string html, params (string Key, string Value)[] fields) =>
        new(fields.Append(("__RequestVerificationToken", WebUtility.HtmlDecode(Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value))).ToDictionary(p => p.Item1, p => p.Item2));

    private static async Task<string> OpenAsync(HttpClient client, string path, string token)
    {
        var html = await client.GetStringAsync(path, Ct);
        var response = await client.PostAsync(path + "?handler=Open", Form(html, ("token", token)), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(Ct);
    }
}
