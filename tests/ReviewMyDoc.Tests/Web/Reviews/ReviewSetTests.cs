// Verifies named, empty and closed collections through real persistence and owner endpoints.
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Reviews;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Contracts for preparing sets before choosing passages or recipients.</summary>
public sealed class ReviewSetTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Empty_named_set_is_persisted_and_rendered_without_a_recipient()
    {
        using var app = new OwnerApplication();
        using var owner = await app.CreateOwnerClientAsync();
        var document = await DocumentAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        var result = await service.CreateSetAsync(document.Document.Id, "  Sprache & Aufbau  ", Ct);
        Assert.Null(result.Error);
        var stored = await service.LoadAsync(document.Document.Id, result.Stored!.Review.Id, Ct);
        Assert.Equal("Sprache & Aufbau", stored!.Review.SetName);
        Assert.Empty(stored.Review.Passages);
        Assert.Equal("Draft", stored.Review.State);
        Assert.Empty(stored.Review.ReviewerName);
        var html = await owner.GetStringAsync($"/dokumente/{document.Document.Id}", Ct);
        Assert.Contains("Sprache &amp; Aufbau", html);
        Assert.Contains("data-review-select", html);
        Assert.Contains("data-set-details", html);
    }

    [Fact]
    public async Task Closed_sets_refuse_stale_and_current_additions_but_can_be_reopened_or_issued()
    {
        using var app = new OwnerApplication();
        var document = await DocumentAsync(app);
        var id = document.Document.Id;
        var documents = app.Services.GetRequiredService<DocumentService>();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        var added = Assert.IsType<DocumentResult.Success>(await documents.AddSectionAsync(id, "Text", document.ETag, Ct));
        var section = added.Document.Sections[0].Id;
        var text = (await store.ReadSectionTextAsync(id, section, Ct))!;
        var written = Assert.IsType<ObjectWriteResult.Written>(await app.Services.GetRequiredService<DocumentEditingService>()
            .SaveAsync(id, section, "Ein klarer Gedanke.", text.ETag, Ct));
        var service = app.Services.GetRequiredService<ReviewService>();
        var initial = (await service.CreateSetAsync(id, "Lesbarkeit", Ct)).Stored!;
        var collected = (await service.CollectAsync(id, section, "Ein klarer Gedanke.", written.ETag.Value, initial.Review.Id, initial.ETag.Value, Ct)).Stored!;
        Assert.NotNull((await service.SetCollectionClosedAsync(id, collected.Review.Id, initial.ETag.Value, true, Ct)).Error);
        var closed = (await service.SetCollectionClosedAsync(id, collected.Review.Id, collected.ETag.Value, true, Ct)).Stored!;
        Assert.NotNull(closed.Review.CollectionClosedAt);
        foreach (var etag in new[] { collected.ETag.Value, closed.ETag.Value })
        {
            Assert.NotNull((await service.CollectAsync(id, section, "Gedanke", written.ETag.Value, closed.Review.Id, etag, Ct)).Error);
        }
        Assert.NotNull((await service.RemoveAsync(id, closed.Review.Id, closed.Review.Passages[0].Id, closed.ETag.Value, Ct)).Error);
        var reopened = (await service.SetCollectionClosedAsync(id, closed.Review.Id, closed.ETag.Value, false, Ct)).Stored!;
        Assert.Null(reopened.Review.CollectionClosedAt);
        closed = (await service.SetCollectionClosedAsync(id, reopened.Review.Id, reopened.ETag.Value, true, Ct)).Stored!;
        var issued = await service.IssueAsync(id, closed.Review.Id, closed.ETag.Value, "Testperson", null, DateTimeOffset.UtcNow.AddDays(7), Ct);
        Assert.Null(issued.Error);
        Assert.Equal("Lesbarkeit", issued.Stored!.Review.SetName);
        Assert.Equal("Sent", issued.Stored.Review.State);
        Assert.NotNull((await service.SetCollectionClosedAsync(id, closed.Review.Id, issued.Stored.ETag.Value, false, Ct)).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Invalid_names_do_not_create_sets(string? name)
    {
        using var app = new OwnerApplication();
        var document = await DocumentAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        Assert.NotNull((await service.CreateSetAsync(document.Document.Id, name, Ct)).Error);
        Assert.NotNull((await service.CreateSetAsync(document.Document.Id, new string('x', 81), Ct)).Error);
        Assert.Empty(await service.ListAsync(document.Document.Id, Ct));
    }

    [Fact]
    public async Task Existing_assignments_without_collection_fields_remain_open_and_readable()
    {
        using var app = new OwnerApplication();
        var document = await DocumentAsync(app);
        var service = app.Services.GetRequiredService<ReviewService>();
        var created = (await service.CreateSetAsync(document.Document.Id, "Altbestand", Ct)).Stored!;
        var json = JsonSerializer.SerializeToNode(created.Review, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        json.Remove("setName");
        json.Remove("collectionClosedAt");
        await app.Services.GetRequiredService<IObjectStore>().WriteAsync($"documents/{document.Document.Id}/reviews/{created.Review.Id}.json",
            json.ToJsonString(), WriteCondition.MustMatch(created.ETag), Ct);
        var loaded = (await service.LoadAsync(document.Document.Id, created.Review.Id, Ct))!;
        Assert.Equal("Review-Set", loaded.Review.SetName);
        Assert.Null(loaded.Review.CollectionClosedAt);
    }

    [Fact]
    public async Task Creation_and_completion_require_antiforgery_and_return_the_persisted_set()
    {
        using var app = new OwnerApplication();
        using var owner = await app.CreateOwnerClientAsync();
        var document = await DocumentAsync(app);
        var url = $"/dokumente/{document.Document.Id}";
        var fields = new Dictionary<string, string> { ["setName"] = "Klarheit" };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync(url + "?handler=CreateSet", new FormUrlEncodedContent(fields), Ct)).StatusCode);
        fields["__RequestVerificationToken"] = OwnerApplication.TokenIn(await owner.GetStringAsync(url, Ct));
        owner.DefaultRequestHeaders.Add("X-Requested-With", "fetch");
        var response = await owner.PostAsync(url + "?handler=CreateSet", new FormUrlEncodedContent(fields), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        fields["reviewId"] = created.RootElement.GetProperty("id").GetString()!;
        fields["etag"] = created.RootElement.GetProperty("etag").GetString()!;
        fields["closed"] = "true";
        fields.Remove("__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync(url + "?handler=CloseSet", new FormUrlEncodedContent(fields), Ct)).StatusCode);
        fields["__RequestVerificationToken"] = OwnerApplication.TokenIn(await owner.GetStringAsync(url, Ct));
        response = await owner.PostAsync(url + "?handler=CloseSet", new FormUrlEncodedContent(fields), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var closed = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(closed.RootElement.GetProperty("closed").GetBoolean());
        Assert.Empty(closed.RootElement.GetProperty("passages").EnumerateArray());
    }

    private static async Task<DocumentResult.Success> DocumentAsync(OwnerApplication app) =>
        Assert.IsType<DocumentResult.Success>(await app.Services.GetRequiredService<DocumentService>().CreateDocumentAsync("owner", "Beispiel", Ct));
}
