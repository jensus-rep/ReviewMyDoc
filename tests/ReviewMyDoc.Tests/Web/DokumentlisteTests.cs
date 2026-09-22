// Checks the list of documents at /dokumente: the designed empty state that
// says what to do next, and that a document that was created really shows up
// with its state and the date it last changed. Every test starts an
// application of its own, each with the temporary storage directory
// OwnerApplication builds for it, so one test can never see the documents of
// another.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Documents;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Integration tests of <see cref="ReviewMyDoc.Web.Pages.Dokumente.IndexModel"/>.</summary>
public sealed class DokumentlisteTests
{
    // Nobody has written a document yet, so the page has to say what to do
    // next rather than show an empty page - the criterion of
    // docs/Konventionen.md this page carries.
    [Fact]
    public async Task An_empty_list_says_what_to_do_next()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();

        var response = await client.GetAsync("/dokumente", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Es gibt noch keine Dokumente.", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/dokumente/anlegen\"", html, StringComparison.Ordinal);
    }

    // A document that exists shows its title and its state in the German word
    // docs/Konzept.md uses for it, not the English name the domain carries.
    [Fact]
    public async Task A_document_shows_its_title_and_its_state()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();
        var documents = application.Services.GetRequiredService<DocumentService>();
        await documents.CreateDocumentAsync("owner", "Gutachten Musterstraße", TestContext.Current.CancellationToken);

        var response = await client.GetAsync("/dokumente", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Gutachten Musterstraße", html, StringComparison.Ordinal);
        Assert.Contains("Entwurf", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Es gibt noch keine Dokumente.", html, StringComparison.Ordinal);
    }

    // The link into the frame's own navigation, so the list is reachable from
    // every page and not only by typing the address.
    [Fact]
    public async Task The_navigation_of_the_frame_carries_the_link_to_the_list()
    {
        using var application = new OwnerApplication();
        using var client = await application.CreateOwnerClientAsync();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains("href=\"/dokumente\"", html, StringComparison.Ordinal);
    }
}
