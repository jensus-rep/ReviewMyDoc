// Checks document creation, loading, listing and renaming against real storage.

using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Documents;

/// <summary>Checks document creation, loading, listing and renaming against real storage.</summary>
public sealed class DocumentServiceTests : DocumentServiceTestBase
{
    // The heart of the task: what lands on disk is what docs/Datenmodell.md
    // describes, field by field, in its order and in its spelling. Compared as
    // text and not as a parsed object on purpose - a comparison of objects would
    // pass however the file is laid out, and the file is the promise here. The
    // two identifiers are the only values taken from the result, because they
    // are drawn; everything else is stated.
    [Fact]
    public async Task The_written_file_is_the_one_the_data_model_describes()
    {
        var created = await CreateDocumentAsync("Gutachten Musterstraße");
        _clock.UtcNow = Changed;
        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));

        var documentId = withSection.Document.Id.Value;
        var sectionId = withSection.Document.Sections[0].Id.Value;
        var content = await ReadEntryAsync($"documents/{documentId}/document.json");

        Assert.Equal(
            $$"""
            {
              "id": "{{documentId}}",
              "ownerId": "owner",
              "title": "Gutachten Musterstraße",
              "state": "Draft",
              "version": 0,
              "sections": [
                {
                  "id": "{{sectionId}}",
                  "heading": "Ausgangslage",
                  "order": 1,
                  "updatedAt": "2026-09-22T08:14:00Z"
                }
              ],
              "createdAt": "2026-09-19T10:00:00Z",
              "updatedAt": "2026-09-22T08:14:00Z"
            }
            """,
            content);
    }

    // The file is written in one place and read in many, among them a Linux web
    // app and a Windows developer machine. A carriage return that crept in from
    // the platform would make every file differ between the two and every
    // comparison of a frozen state noisy.
    [Fact]
    public async Task The_written_file_carries_no_carriage_return()
    {
        var created = await CreateDocumentAsync("Gutachten");

        var content = await ReadEntryAsync($"documents/{created.Document.Id}/document.json");

        Assert.DoesNotContain("\r", content);
    }

    [Fact]
    public async Task A_new_document_is_a_draft_at_version_zero_without_sections()
    {
        var created = await CreateDocumentAsync("Gutachten");

        Assert.Equal("owner", created.Document.OwnerId);
        Assert.Equal("Gutachten", created.Document.Title);
        Assert.Equal(DocumentState.Draft, created.Document.State);
        Assert.Equal(0, created.Document.Version);
        Assert.Empty(created.Document.Sections);
        Assert.Equal(Created, created.Document.CreatedAt);
        Assert.Equal(Created, created.Document.UpdatedAt);
    }

    // The identifier reaches the outside world in a link, so it has to be drawn
    // and not counted up, and it has to fit what the object store accepts as a
    // path. A store that refused it would only say so when the first document is
    // saved.
    [Fact]
    public async Task Drawn_identifiers_are_url_safe_and_differ_from_one_another()
    {
        var first = await CreateDocumentAsync("Erstes");
        var second = await CreateDocumentAsync("Zweites");
        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(first.Document.Id, "Ausgangslage", first.ETag, Token));

        Assert.NotEqual(first.Document.Id, second.Document.Id);
        foreach (var identifier in new[]
        {
            first.Document.Id.Value,
            second.Document.Id.Value,
            withSection.Document.Sections[0].Id.Value,
        })
        {
            Assert.NotEmpty(identifier);
            Assert.All(identifier, character =>
                Assert.True(
                    character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_',
                    $"'{identifier}' carries a character that no path of the model may hold."));
        }
    }

    [Fact]
    public async Task A_document_reads_back_as_it_was_written()
    {
        var created = await CreateDocumentAsync("Gutachten Musterstraße");
        _clock.UtcNow = Changed;
        var written = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));

        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(created.Document.Id, Token));

        Assert.Equal("Gutachten Musterstraße", loaded.Document.Title);
        Assert.Equal(written.ETag, loaded.ETag);
        var section = Assert.Single(loaded.Document.Sections);
        Assert.Equal(written.Document.Sections[0].Id, section.Id);
        Assert.Equal("Ausgangslage", section.Heading);
        Assert.Equal(1, section.Order);
        Assert.Equal(Changed, section.UpdatedAt);
    }

    [Fact]
    public async Task Loading_a_document_that_is_not_there_reports_it()
    {
        var result = await _service.LoadDocumentAsync(DocumentIdentifier.Draw(), Token);

        Assert.IsType<DocumentResult.DocumentNotFound>(result);
    }

    // The list Pages/Dokumente/Index.cshtml shows when nobody has written
    // anything yet: no document, and an empty list is the honest answer, not a
    // failure.
    [Fact]
    public async Task Listing_documents_when_there_are_none_returns_an_empty_list()
    {
        var documents = await _service.ListDocumentsAsync(Token);

        Assert.Empty(documents);
    }

    // The one rule of the operation: newest change first, regardless of the
    // order the documents were created in.
    [Fact]
    public async Task Listing_documents_returns_every_one_newest_change_first()
    {
        var first = await CreateDocumentAsync("Zuerst angelegt");
        _clock.UtcNow = Changed;
        var second = await CreateDocumentAsync("Zuletzt angelegt");

        // The first document is renamed after the second was created, so the
        // moment it last changed is now the later one - the list has to follow
        // that and not the order the two were created in.
        _clock.UtcNow = Changed + TimeSpan.FromDays(1);
        var renamed = await SucceedsAsync(
            _service.RenameDocumentAsync(first.Document.Id, "Zuerst angelegt, zuletzt geändert", first.ETag, Token));

        var documents = await _service.ListDocumentsAsync(Token);

        Assert.Equal(
            [renamed.Document.Id, second.Document.Id],
            documents.Select(document => document.Id));
    }

    // The list holds one entry per document, however many other entries - here
    // three section texts - that document owns under the same prefix.
    [Fact]
    public async Task Listing_documents_with_sections_still_lists_each_document_once()
    {
        await WithThreeSectionsAsync();

        var documents = await _service.ListDocumentsAsync(Token);

        Assert.Single(documents);
    }

    [Fact]
    public async Task Renaming_a_document_changes_its_title_and_nothing_else()
    {
        var created = await CreateDocumentAsync("Gutachten");
        _clock.UtcNow = Changed;

        var renamed = await SucceedsAsync(
            _service.RenameDocumentAsync(created.Document.Id, "Gutachten Musterstraße", created.ETag, Token));

        Assert.Equal("Gutachten Musterstraße", renamed.Document.Title);
        Assert.Equal(Created, renamed.Document.CreatedAt);
        Assert.Equal(Changed, renamed.Document.UpdatedAt);
        Assert.NotEqual(created.ETag, renamed.ETag);
    }

    [Fact]
    public async Task Renaming_a_document_that_is_not_there_reports_it()
    {
        var created = await CreateDocumentAsync("Gutachten");

        var result = await _service.RenameDocumentAsync(
            DocumentIdentifier.Draw(),
            "Anderer Titel",
            created.ETag,
            Token);

        Assert.IsType<DocumentResult.DocumentNotFound>(result);
    }
}
