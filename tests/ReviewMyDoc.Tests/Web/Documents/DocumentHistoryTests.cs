// Tests rendered history and real authorization. A reviewer's valid capability
// must never expose owner history, even for the assigned document.
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Audit;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Reviews;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Tests.Audit;
using ReviewMyDoc.Tests.Documents;

namespace ReviewMyDoc.Tests.Web;

/// <summary>History resolves names safely and stays within the owner boundary.</summary>
public sealed class DocumentHistoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owner_sees_newest_first_sentences_names_deleted_identifiers_and_safe_markup()
    {
        using var app = new OwnerApplication();
        var service = app.Services.GetRequiredService<DocumentService>();
        var created = Assert.IsType<DocumentResult.Success>(await service.CreateDocumentAsync("owner", "Meine Historie", Ct));
        var id = created.Document.Id;
        var added = Assert.IsType<DocumentResult.Success>(await service.AddSectionAsync(id, "Alter Name", created.ETag, Ct));
        var section = added.Document.Sections[0].Id;
        var second = Assert.IsType<DocumentResult.Success>(await service.AddSectionAsync(id, "Gelöschter Abschnitt", added.ETag, Ct));
        var deletedId = second.Document.Sections[1].Id;
        var renamed = Assert.IsType<DocumentResult.Success>(await service.RenameSectionAsync(id, section, "Aktueller Name <script>privat</script>", second.ETag, Ct));
        await service.DeleteSectionAsync(id, deletedId, renamed.ETag, Ct);
        var reviews = app.Services.GetRequiredService<ReviewStore>();
        var reviewId = DocumentIdentifier.Draw().Value;
        var review = new ReviewAssignment(reviewId, id.Value, "Meine Historie", "Returned", [], DateTimeOffset.UtcNow,
            ReviewerName: "Anna <img src=x onerror=alert(1)>");
        await reviews.WriteAsync(review, WriteCondition.MustNotExist, Ct);
        var future = new FixedTimeProvider(new DateTimeOffset(2040, 1, 2, 3, 4, 5, TimeSpan.Zero));
        await new AuditLog(app.Services.GetRequiredService<IObjectStore>(), future, new RecordingAuditFailures())
            .RecordAsync(id, AuditEventKind.ReviewReturned, Ct, reviewId: reviewId, actor: AuditActor.Reviewer);
        await service.CreateDocumentAsync("owner", "UNRELATED DOCUMENT", Ct);
        using var owner = await app.CreateOwnerClientAsync();
        var response = await owner.GetAsync($"/dokumente/{id}/historie", Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Content-Security-Policy", response.Headers.Select(h => h.Key));
        Assert.Contains("Dokumenthistorie", html);
        Assert.Contains("Anna &lt;img", html);
        Assert.Contains("Aktueller Name &lt;script&gt;privat&lt;/script&gt;", html);
        Assert.Contains(deletedId.Value, html);
        Assert.Contains("Du hast das Dokument angelegt.", html);
        Assert.True(html.IndexOf("zurückgemeldet", StringComparison.Ordinal) < html.IndexOf("das Dokument angelegt", StringComparison.Ordinal));
        Assert.DoesNotContain("<script>privat", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.DoesNotContain("UNRELATED DOCUMENT", html);
        Assert.DoesNotContain("actorId", html);
        Assert.Contains($"/dokumente/{id}/historie", await owner.GetStringAsync($"/dokumente/{id}", Ct));
    }

    [Fact]
    public async Task Older_documents_have_empty_history_and_unknown_documents_are_404()
    {
        using var app = new OwnerApplication();
        var document = Document.Create(DocumentIdentifier.Draw(), "owner", "Älteres Dokument", DateTimeOffset.UtcNow);
        await app.Services.GetRequiredService<IDocumentStore>().WriteAsync(document, WriteCondition.MustNotExist, Ct);
        using var owner = await app.CreateOwnerClientAsync();
        var html = await owner.GetStringAsync($"/dokumente/{document.Id}/historie", Ct);
        Assert.Contains("noch keine Ereignisse aufgezeichnet", html);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/dokumente/{DocumentIdentifier.Draw()}/historie", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/dokumente/INVALID/historie", Ct)).StatusCode);
    }

    [Fact]
    public async Task Neither_anonymous_nor_authenticated_reviewers_can_read_owner_history()
    {
        using var app = new OwnerApplication();
        var documents = app.Services.GetRequiredService<DocumentService>();
        var created = Assert.IsType<DocumentResult.Success>(await documents.CreateDocumentAsync("owner", "OWNER HISTORY SECRET", Ct));
        var id = created.Document.Id;
        var added = Assert.IsType<DocumentResult.Success>(await documents.AddSectionAsync(id, "Assigned section", created.ETag, Ct));
        var section = added.Document.Sections[0].Id;
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var blank = (await store.ReadSectionTextAsync(id, section, Ct))!;
        var saved = Assert.IsType<ObjectWriteResult.Written>(await app.Services.GetRequiredService<DocumentEditingService>()
            .SaveAsync(id, section, "Assigned text", blank.ETag, Ct));
        var reviews = app.Services.GetRequiredService<ReviewService>();
        var draft = await reviews.CollectAsync(id, section, "Assigned text", saved.ETag.Value, null, null, Ct);
        var issued = await reviews.IssueAsync(id, draft.Stored!.Review.Id, draft.Stored.ETag.Value, "Reviewer", null, DateTimeOffset.UtcNow.AddDays(7), Ct);
        using var reviewer = app.CreateAnonymousClient(followRedirects: false);
        var historyPath = $"/dokumente/{id}/historie";
        var anonymous = await reviewer.GetAsync(historyPath, Ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.DoesNotContain("OWNER HISTORY SECRET", await anonymous.Content.ReadAsStringAsync(Ct));
        var reviewPath = $"/review/{id}/{issued.Stored!.Review.Id}";
        var invitation = await reviewer.GetStringAsync(reviewPath, Ct);
        var opened = await reviewer.PostAsync(reviewPath + "?handler=Open", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = issued.Token!,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(OwnerApplication.TokenIn(invitation))
        }), Ct);
        Assert.Equal(HttpStatusCode.Redirect, opened.StatusCode);
        Assert.Contains("Assigned text", await reviewer.GetStringAsync(reviewPath, Ct));
        var denied = await reviewer.GetAsync(historyPath, Ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/anmeldung", denied.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        var body = await denied.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("Du hast", body);
        Assert.DoesNotContain("OWNER HISTORY SECRET", body);
    }
}
