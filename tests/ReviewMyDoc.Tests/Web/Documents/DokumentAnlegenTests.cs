// Checks the form that creates a document at /dokumente/anlegen: the
// antiforgery token, the refusal of an empty title, and that a real title ends
// on the outline of the new document - a page the next task builds, so this
// checks the Location header and not the target, exactly as the task allows.

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Web.Pages.Dokumente;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Integration tests of <see cref="AnlegenModel"/>.</summary>
public sealed class DokumentAnlegenTests
{
    // The form is reachable only by a signed-in owner, and it carries the token
    // without which the post is refused.
    [Fact]
    public async Task The_form_is_shown_with_an_antiforgery_token()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();

        var response = await client.GetAsync("/dokumente/anlegen", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Title\"", html, StringComparison.Ordinal);
    }

    // A post without the token never reaches the page model, exactly as for
    // every other form of this application.
    [Fact]
    public async Task A_post_without_the_antiforgery_token_is_refused()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();

        var form = new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("Title", "Gutachten")]);

        var response = await client.PostAsync(
            "/dokumente/anlegen",
            form,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The criterion the task names first: an empty title is refused, with the
    // form and the message beside the field, not with an exception page.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_title_is_refused(string title)
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();
        var token = await OwnerApplication.AntiforgeryTokenAsync(client, "/dokumente/anlegen");

        var response = await PostAsync(client, token, title);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(AnlegenModel.EmptyTitleMessage, html, StringComparison.Ordinal);
    }

    // The heart of the task: a real title creates the document and leads
    // straight to the outline of the new document, without a detour through
    // the list.
    [Fact]
    public async Task A_real_title_creates_the_document_and_redirects_to_its_outline()
    {
        using var application = new OwnerApplication();
        using var client = application.CreateAnonymousClient(followRedirects: false);
        var loginToken = await OwnerApplication.AntiforgeryTokenAsync(client, OwnerAuthentication.LoginPath);
        await OwnerApplication.SignInAsync(client, loginToken, OwnerApplication.Password);
        var token = await OwnerApplication.AntiforgeryTokenAsync(client, "/dokumente/anlegen");

        var response = await PostAsync(client, token, "Gutachten Musterstraße");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.True(
            Regex.IsMatch(location, "^/dokumente/[a-z0-9_]+$"),
            $"'{location}' does not name the outline of a document.");

        var documents = application.Services.GetRequiredService<DocumentService>();
        var documentId = new DocumentIdentifier(location.Split('/')[2]);
        var loaded = Assert.IsType<DocumentResult.Success>(
            await documents.LoadDocumentAsync(documentId, TestContext.Current.CancellationToken));
        Assert.Equal("Gutachten Musterstraße", loaded.Document.Title);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string title)
    {
        var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Title", title),
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        ]);

        return client.PostAsync("/dokumente/anlegen", form, TestContext.Current.CancellationToken);
    }
}
