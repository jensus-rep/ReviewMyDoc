// Exercises the real persistence and HTTP boundary from writing through review
// return. Capabilities, unrelated content and stale edits are tested explicitly.
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Reviews;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Tests.Web;

/// <summary>End-to-end contracts of the writing and review workflow.</summary>
public sealed class ReviewWorkflowTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Editor_saves_real_text_and_refuses_a_stale_tab()
    {
        using var app = new OwnerApplication();
        using var owner = await app.CreateOwnerClientAsync();
        var (id, section, text) = await CreateAsync(app);
        var page = await owner.GetStringAsync($"/dokumente/{id}", Ct);
        Assert.Contains("data-editor-surface", page);
        Assert.Contains("Für das Review", page);
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["sectionId"] = section.Value,
            ["etag"] = text.ETag.Value,
            ["markdown"] = "## Neue Fassung\n\nEin **wichtiger** Gedanke.",
        };
        var response = await owner.PostAsync($"/dokumente/{id}?handler=Save", new FormUrlEncodedContent(fields), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        fields["markdown"] = "Veralteter Text";
        var stale = await owner.PostAsync($"/dokumente/{id}?handler=Save", new FormUrlEncodedContent(fields), Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var stored = await app.Services.GetRequiredService<IDocumentStore>().ReadSectionTextAsync(id, section, Ct);
        Assert.Contains("wichtiger", stored!.Content);
        Assert.DoesNotContain("Veralteter", stored.Content);
    }

    [Fact]
    public async Task Collected_passages_are_private_until_issued_and_feedback_returns_to_the_pipeline()
    {
        using var app = new OwnerApplication();
        using var owner = await app.CreateOwnerClientAsync();
        var (id, section, text) = await CreateAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        var collected = await service.CollectAsync(id, section, "Öffentliche Passage", text.ETag.Value, null, null, Ct);
        var draft = collected.Stored!;
        Assert.Null(collected.Error);
        Assert.Null(await service.AccessAsync(id, draft.Review.Id, new string('a', 64), Ct));
        var ownerPage = await owner.GetStringAsync($"/dokumente/{id}/reviews/{draft.Review.Id}", Ct);
        Assert.Contains("Wer soll mitlesen?", ownerPage);

        var issued = await service.IssueAsync(id, draft.Review.Id, draft.ETag.Value, "Anna", "", DateTimeOffset.UtcNow.AddDays(7), Ct);
        Assert.Null(issued.Error);
        Assert.NotNull(issued.Token);
        var path = $"/review/{id}/{draft.Review.Id}";
        using var reviewer = app.CreateAnonymousClient();
        var before = await reviewer.GetStringAsync(path, Ct);
        Assert.DoesNotContain("Öffentliche Passage", before);
        var opened = await reviewer.PostAsync(path + "?handler=Open", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = Token(before), ["token"] = issued.Token! }), Ct);
        var html = await opened.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Contains("Öffentliche Passage", html);
        Assert.DoesNotContain("VERTRAULICHER REST", html);
        Assert.DoesNotContain(issued.Token!, html);
        var passage = draft.Review.Passages[0];
        var returned = await reviewer.PostAsync(path + "?handler=Return", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html),
            ["etag"] = issued.Stored!.ETag.Value,
            [$"Feedback[{passage.Id}]"] = "Bitte die Aussage konkretisieren.",
        }), Ct);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var current = await service.LoadAsync(id, draft.Review.Id, Ct);
        Assert.Equal("Returned", current!.Review.State);
        Assert.Equal("Zurück", ReviewMyDoc.Web.Pages.IndexModel.Stage(current.Review));
        Assert.NotNull((await service.DecideAsync(id, draft.Review.Id, current.ETag.Value, "accept", null, Ct)).Error);
        var resolved = await service.DecideAsync(id, draft.Review.Id, current.ETag.Value, "resolve", passage.Id, Ct);
        Assert.Equal("Bei dir", ReviewMyDoc.Web.Pages.IndexModel.Stage(resolved.Stored!.Review));
        var accepted = await service.DecideAsync(id, draft.Review.Id, resolved.Stored.ETag.Value, "accept", null, Ct);
        Assert.Equal("Erledigt", ReviewMyDoc.Web.Pages.IndexModel.Stage(accepted.Stored!.Review));
        var pipeline = await owner.GetStringAsync("/", Ct);
        Assert.Contains("Anna", pipeline);
        Assert.Contains("Erledigt", pipeline);
    }

    [Fact]
    public async Task Changed_sources_foreign_feedback_and_revoked_capabilities_are_refused()
    {
        using var app = new OwnerApplication();
        var (id, section, text) = await CreateAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        var collected = await service.CollectAsync(id, section, "Öffentliche Passage", text.ETag.Value, null, null, Ct);
        var draft = collected.Stored!;
        var issued = await service.IssueAsync(id, draft.Review.Id, draft.ETag.Value, "Anna", null, DateTimeOffset.UtcNow.AddDays(7), Ct);
        var foreign = await service.ReturnAsync(id, draft.Review.Id, ReviewService.Hash(issued.Token!), issued.Stored!.ETag.Value,
            new Dictionary<string, string> { ["foreign"] = "Not allowed" }, Ct);
        Assert.NotNull(foreign.Error);
        var revoked = await service.DecideAsync(id, draft.Review.Id, issued.Stored.ETag.Value, "revoke", null, Ct);
        Assert.Null(revoked.Error);
        Assert.Null(await service.AccessAsync(id, draft.Review.Id, ReviewService.Hash(issued.Token!), Ct));

        var second = await service.CollectAsync(id, section, "Öffentliche Passage", text.ETag.Value, null, null, Ct);
        await app.Services.GetRequiredService<DocumentEditingService>().SaveAsync(id, section, "Verändert", text.ETag, Ct);
        var refused = await service.IssueAsync(id, second.Stored!.Review.Id, second.Stored.ETag.Value, "Anna", null, DateTimeOffset.UtcNow.AddDays(7), Ct);
        Assert.NotNull(refused.Error);
        Assert.Null(refused.Token);
    }

    [Fact]
    public async Task Expired_capabilities_and_stale_review_updates_are_refused()
    {
        using var app = new OwnerApplication();
        var (id, section, text) = await CreateAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        var collected = await service.CollectAsync(id, section, "Öffentliche Passage", text.ETag.Value, null, null, Ct);
        var draft = collected.Stored!;
        var added = await service.CollectAsync(id, section, "Weitere Passage", text.ETag.Value, draft.Review.Id, draft.ETag.Value, Ct);
        Assert.Null(added.Error);
        Assert.NotNull((await service.RemoveAsync(id, draft.Review.Id, draft.Review.Passages[0].Id, draft.ETag.Value, Ct)).Error);
        var issued = await service.IssueAsync(id, draft.Review.Id, added.Stored!.ETag.Value, "Anna", null, DateTimeOffset.UtcNow.AddDays(7), Ct);
        Assert.Null(issued.Error);
        Assert.Null(await service.AccessAsync(id, draft.Review.Id, ReviewService.Hash("wrong-token"), Ct));
        var expired = issued.Stored!.Review with { TokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) };
        await app.Services.GetRequiredService<ReviewStore>().WriteAsync(expired, WriteCondition.MustMatch(issued.Stored.ETag), Ct);
        Assert.Null(await service.AccessAsync(id, draft.Review.Id, ReviewService.Hash(issued.Token!), Ct));
    }

    [Fact]
    public async Task Collect_endpoint_requires_a_saved_source_and_an_antiforgery_token()
    {
        using var app = new OwnerApplication();
        using var owner = await app.CreateOwnerClientAsync();
        var (id, section, text) = await CreateAsync(app);
        var fields = new Dictionary<string, string> { ["sectionId"] = section.Value, ["markdown"] = "Passage", ["etag"] = text.ETag.Value };
        var rejected = await owner.PostAsync($"/dokumente/{id}?handler=Collect", new FormUrlEncodedContent(fields), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        fields["__RequestVerificationToken"] = Token(await owner.GetStringAsync($"/dokumente/{id}", Ct));
        var accepted = await owner.PostAsync($"/dokumente/{id}?handler=Collect", new FormUrlEncodedContent(fields), Ct);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Contains("passages", await accepted.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<(DocumentIdentifier Id, SectionIdentifier Section, StoredSectionText Text)> CreateAsync(OwnerApplication app)
    {
        var documents = app.Services.GetRequiredService<DocumentService>();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var created = Assert.IsType<DocumentResult.Success>(await documents.CreateDocumentAsync("owner", "Unser Konzept", Ct));
        var added = Assert.IsType<DocumentResult.Success>(await documents.AddSectionAsync(created.Document.Id, "Dokumenttext", created.ETag, Ct));
        var section = added.Document.Sections[0].Id;
        var blank = await store.ReadSectionTextAsync(created.Document.Id, section, Ct);
        await app.Services.GetRequiredService<DocumentEditingService>().SaveAsync(created.Document.Id, section,
            "Öffentliche Passage\n\nVERTRAULICHER REST", blank!.ETag, Ct);
        return (created.Document.Id, section, (await store.ReadSectionTextAsync(created.Document.Id, section, Ct))!);
    }

    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
}
