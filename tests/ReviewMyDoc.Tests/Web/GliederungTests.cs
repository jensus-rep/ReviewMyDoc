// Checks the outline of a document at /dokumente/{documentId}/gliederung: creating,
// renaming, reordering and deleting a section, the confirmation the deletion
// asks for before it happens, the conflict message when the document changed
// underneath the form, and the two things every page of this application has
// to carry regardless of what it shows - antiforgery and a 404 for an address
// that names nothing.

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Web.Pages.Dokumente;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Integration tests of <see cref="GliederungModel"/>.</summary>
public sealed class GliederungTests
{
    // A link can name a document that was never created, and an identifier
    // that a form never produces - both are ordinary 404s, never an exception
    // page.
    [Theory]
    [InlineData("abcdefabcdef")]
    [InlineData("ABCDEFabcdef")]
    public async Task An_unknown_or_malformed_document_id_results_in_404(string documentId)
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();

        var response = await client.GetAsync($"/dokumente/{documentId}/gliederung", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Every form of the page carries its own antiforgery token, and the
    // heading of a section already there is shown.
    [Fact]
    public async Task The_outline_shows_its_sections_with_an_antiforgery_token_in_every_form()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();
        var documentId = await CreateDocumentWithSectionAsync(application, "Ausgangslage");

        var response = await client.GetAsync($"/dokumente/{documentId}/gliederung", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ausgangslage", html, StringComparison.Ordinal);
        Assert.Contains("handler=AddSection", html, StringComparison.Ordinal);
        Assert.Contains("handler=RenameSection", html, StringComparison.Ordinal);
        Assert.Contains("handler=MoveUp", html, StringComparison.Ordinal);
        Assert.Contains("handler=MoveDown", html, StringComparison.Ordinal);

        // One document with one section shows four forms: append, rename,
        // move up and move down. The fifth token belongs to the sign-out form
        // every page carries in its frame.
        var tokenCount = Regex.Matches(html, "name=\"__RequestVerificationToken\"").Count;
        Assert.Equal(5, tokenCount);
    }

    // Every section carries the same four captions. Somebody who hears the page
    // instead of seeing it would get "Nach oben" five times in a row and no way
    // to tell which section each one moves, so every one of them says its
    // section by name.
    [Fact]
    public async Task Every_button_of_a_section_names_the_section_it_belongs_to()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();
        var documentId = await CreateDocumentWithSectionAsync(application, "Ausgangslage");

        var html = await client.GetStringAsync($"/dokumente/{documentId}/gliederung", TestContext.Current.CancellationToken);

        Assert.Contains("aria-label=\"Überschrift von „Ausgangslage“ speichern\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"„Ausgangslage“ nach oben verschieben\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"„Ausgangslage“ nach unten verschieben\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"„Ausgangslage“ löschen\"", html, StringComparison.Ordinal);
    }

    // The heart of the task: a real heading appends a section, and the page
    // redirects back to the outline instead of showing a stale form.
    [Fact]
    public async Task A_real_heading_appends_a_section_and_redirects_back_to_the_outline()
    {
        using var application = new OwnerApplication();
        using var client = await CreateSignedInClientAsync(application, followRedirects: false);
        var documentId = await CreateDocumentAsync(application);
        var (token, eTag) = await LoadFormContextAsync(client, $"/dokumente/{documentId}/gliederung");

        var response = await PostAsync(
            client,
            $"/dokumente/{documentId}/gliederung?handler=AddSection",
            token,
            [new KeyValuePair<string, string>("NewHeading", "Ausgangslage"), new("ETagValue", eTag)]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var documents = application.Services.GetRequiredService<DocumentService>();
        var loaded = Assert.IsType<DocumentResult.Success>(
            await documents.LoadDocumentAsync(new DocumentIdentifier(documentId), TestContext.Current.CancellationToken));
        Assert.Equal("Ausgangslage", Assert.Single(loaded.Document.Sections).Heading);
    }

    // The criterion the task names first for the empty case: a blank heading
    // is refused with a sentence beside the field, not with an exception.
    [Fact]
    public async Task An_empty_heading_is_refused_with_a_message_beside_the_field()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();
        var documentId = await CreateDocumentAsync(application);
        var (token, eTag) = await LoadFormContextAsync(client, $"/dokumente/{documentId}/gliederung");

        var response = await PostAsync(
            client,
            $"/dokumente/{documentId}/gliederung?handler=AddSection",
            token,
            [new KeyValuePair<string, string>("NewHeading", "   "), new("ETagValue", eTag)]);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(GliederungModel.EmptyHeadingMessage, html, StringComparison.Ordinal);
    }

    // Renaming touches only the heading; the identifier the section was given
    // when it was created never appears in a form and cannot drift.
    [Fact]
    public async Task Renaming_a_section_changes_its_heading()
    {
        using var application = new OwnerApplication();
        using var client = await CreateSignedInClientAsync(application, followRedirects: false);
        var documents = application.Services.GetRequiredService<DocumentService>();
        var documentId = await CreateDocumentWithSectionAsync(application, "Ausgangslage");
        var loaded = Assert.IsType<DocumentResult.Success>(
            await documents.LoadDocumentAsync(new DocumentIdentifier(documentId), TestContext.Current.CancellationToken));
        var sectionId = Assert.Single(loaded.Document.Sections).Id.Value;
        var (token, eTag) = await LoadFormContextAsync(client, $"/dokumente/{documentId}/gliederung");

        var response = await PostAsync(
            client,
            $"/dokumente/{documentId}/gliederung?handler=RenameSection",
            token,
            [
                new KeyValuePair<string, string>("RenameSectionId", sectionId),
                new("RenameHeading", "Neue Ausgangslage"),
                new("ETagValue", eTag),
            ]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var reloaded = Assert.IsType<DocumentResult.Success>(
            await documents.LoadDocumentAsync(new DocumentIdentifier(documentId), TestContext.Current.CancellationToken));
        Assert.Equal("Neue Ausgangslage", Assert.Single(reloaded.Document.Sections).Heading);
    }

    // The one way to change the order this task allows: buttons, checked here
    // without a mouse and without a script running behind them.
    [Fact]
    public async Task Moving_a_section_up_swaps_it_with_the_one_before_it()
    {
        using var application = new OwnerApplication();
        using var client = await CreateSignedInClientAsync(application, followRedirects: false);
        var documents = application.Services.GetRequiredService<DocumentService>();
        var (documentId, firstId, secondId) = await CreateDocumentWithTwoSectionsAsync(application, "Erste", "Zweite");
        var id = new DocumentIdentifier(documentId);
        var (token, eTag) = await LoadFormContextAsync(client, $"/dokumente/{documentId}/gliederung");

        var response = await PostAsync(
            client,
            $"/dokumente/{documentId}/gliederung?handler=MoveUp",
            token,
            [
                new KeyValuePair<string, string>("SectionId", secondId),
                new("ETagValue", eTag),
                new("SectionOrder", firstId),
                new("SectionOrder", secondId),
            ]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var reloaded = Assert.IsType<DocumentResult.Success>(
            await documents.LoadDocumentAsync(id, TestContext.Current.CancellationToken));
        Assert.Equal("Zweite", reloaded.Document.Sections[0].Heading);
        Assert.Equal("Erste", reloaded.Document.Sections[1].Heading);
    }

    // Deleting asks first: the section is still there after the link is
    // followed, and only the confirmation named it as pending.
    [Fact]
    public async Task Following_the_delete_link_shows_a_confirmation_and_deletes_nothing_yet()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();
        var documents = application.Services.GetRequiredService<DocumentService>();
        var documentId = await CreateDocumentWithSectionAsync(application, "Ausgangslage");
        var loaded = Assert.IsType<DocumentResult.Success>(
            await documents.LoadDocumentAsync(new DocumentIdentifier(documentId), TestContext.Current.CancellationToken));
        var sectionId = Assert.Single(loaded.Document.Sections).Id.Value;

        var response = await client.GetAsync(
            $"/dokumente/{documentId}/gliederung?confirmDelete={sectionId}",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("wirklich löschen", html, StringComparison.Ordinal);
        Assert.Contains("handler=ConfirmDelete", html, StringComparison.Ordinal);
        Assert.DoesNotContain("confirm(", html, StringComparison.Ordinal);

        var stillThere = Assert.IsType<DocumentResult.Success>(
            await documents.LoadDocumentAsync(new DocumentIdentifier(documentId), TestContext.Current.CancellationToken));
        Assert.Single(stillThere.Document.Sections);
    }

    // Confirming the deletion is what actually removes the section.
    [Fact]
    public async Task Confirming_the_delete_removes_the_section()
    {
        using var application = new OwnerApplication();
        using var client = await CreateSignedInClientAsync(application, followRedirects: false);
        var documents = application.Services.GetRequiredService<DocumentService>();
        var documentId = await CreateDocumentWithSectionAsync(application, "Ausgangslage");
        var loaded = Assert.IsType<DocumentResult.Success>(
            await documents.LoadDocumentAsync(new DocumentIdentifier(documentId), TestContext.Current.CancellationToken));
        var sectionId = Assert.Single(loaded.Document.Sections).Id.Value;
        var (token, eTag) = await LoadFormContextAsync(client, $"/dokumente/{documentId}/gliederung?confirmDelete={sectionId}");

        var response = await PostAsync(
            client,
            $"/dokumente/{documentId}/gliederung?handler=ConfirmDelete",
            token,
            [new KeyValuePair<string, string>("SectionId", sectionId), new("ETagValue", eTag)]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var reloaded = Assert.IsType<DocumentResult.Success>(
            await documents.LoadDocumentAsync(new DocumentIdentifier(documentId), TestContext.Current.CancellationToken));
        Assert.Empty(reloaded.Document.Sections);
    }

    // The criterion at the heart of docs/Datenmodell.md: a document changed by
    // somebody else between load and save is reported, not overwritten, and
    // what was typed is still there for the visitor to see.
    [Fact]
    public async Task A_conflict_shows_a_message_and_keeps_the_typed_heading()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();
        var documents = application.Services.GetRequiredService<DocumentService>();
        var documentId = await CreateDocumentAsync(application);
        var id = new DocumentIdentifier(documentId);
        var (token, staleETag) = await LoadFormContextAsync(client, $"/dokumente/{documentId}/gliederung");

        // Another tab changes the document after this page was loaded.
        var current = Assert.IsType<DocumentResult.Success>(
            await documents.LoadDocumentAsync(id, TestContext.Current.CancellationToken));
        await documents.AddSectionAsync(id, "Von anderswo", current.ETag, TestContext.Current.CancellationToken);

        var response = await PostAsync(
            client,
            $"/dokumente/{documentId}/gliederung?handler=AddSection",
            token,
            [new KeyValuePair<string, string>("NewHeading", "Mein Vorschlag"), new("ETagValue", staleETag)]);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(GliederungModel.ConflictMessage, html, StringComparison.Ordinal);
        Assert.Contains("value=\"Mein Vorschlag\"", html, StringComparison.Ordinal);
    }

    // The same rule as every other form of this application: no token, no effect.
    [Fact]
    public async Task A_post_without_the_antiforgery_token_is_refused()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();
        var documentId = await CreateDocumentAsync(application);
        var form = new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("NewHeading", "Ausgangslage"), new("ETagValue", "irgendein-wert")]);

        var response = await client.PostAsync(
            $"/dokumente/{documentId}/gliederung?handler=AddSection",
            form,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<string> CreateDocumentAsync(OwnerApplication application)
    {
        var documents = application.Services.GetRequiredService<DocumentService>();
        var created = Assert.IsType<DocumentResult.Success>(
            await documents.CreateDocumentAsync("owner", "Gutachten Musterstraße", TestContext.Current.CancellationToken));

        return created.Document.Id.Value;
    }

    private static async Task<string> CreateDocumentWithSectionAsync(OwnerApplication application, string heading)
    {
        var documents = application.Services.GetRequiredService<DocumentService>();
        var created = Assert.IsType<DocumentResult.Success>(
            await documents.CreateDocumentAsync("owner", "Gutachten Musterstraße", TestContext.Current.CancellationToken));
        await documents.AddSectionAsync(created.Document.Id, heading, created.ETag, TestContext.Current.CancellationToken);

        return created.Document.Id.Value;
    }

    private static async Task<(string DocumentId, string FirstSectionId, string SecondSectionId)> CreateDocumentWithTwoSectionsAsync(
        OwnerApplication application,
        string firstHeading,
        string secondHeading)
    {
        var documents = application.Services.GetRequiredService<DocumentService>();
        var created = Assert.IsType<DocumentResult.Success>(
            await documents.CreateDocumentAsync("owner", "Gutachten Musterstraße", TestContext.Current.CancellationToken));
        var afterFirst = Assert.IsType<DocumentResult.Success>(
            await documents.AddSectionAsync(created.Document.Id, firstHeading, created.ETag, TestContext.Current.CancellationToken));
        var afterSecond = Assert.IsType<DocumentResult.Success>(
            await documents.AddSectionAsync(afterFirst.Document.Id, secondHeading, afterFirst.ETag, TestContext.Current.CancellationToken));

        return (
            afterSecond.Document.Id.Value,
            afterSecond.Document.Sections[0].Id.Value,
            afterSecond.Document.Sections[1].Id.Value);
    }

    private static async Task<(string Token, string ETag)> LoadFormContextAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path, TestContext.Current.CancellationToken);
        var token = OwnerApplication.TokenIn(html);
        var eTagMatch = Regex.Match(html, "name=\"ETagValue\" value=\"([^\"]+)\"");

        Assert.True(eTagMatch.Success, "The page carries no ETagValue hidden field.");

        return (token, eTagMatch.Groups[1].Value);
    }

    // Signs in without following the redirect it produces, so a caller can
    // still see the raw status of the request it actually cares about - the
    // same reason DokumentAnlegenTests keeps a client of its own instead of
    // OwnerApplication's shared, redirect-following one.
    private static async Task<HttpClient> CreateSignedInClientAsync(OwnerApplication application, bool followRedirects)
    {
        var client = application.CreateAnonymousClient(followRedirects);
        var loginToken = await OwnerApplication.AntiforgeryTokenAsync(client, OwnerApplication.LoginPath);
        await OwnerApplication.SignInAsync(client, loginToken, OwnerApplication.Password);

        return client;
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        string token,
        IEnumerable<KeyValuePair<string, string>> fields)
    {
        var form = new FormUrlEncodedContent([.. fields, new KeyValuePair<string, string>("__RequestVerificationToken", token)]);

        return client.PostAsync(path, form, TestContext.Current.CancellationToken);
    }
}
