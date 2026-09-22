// Every operation of DocumentService, in its success case and in the failures
// the task named: an outdated version, a section that does not belong to the
// document, an order that does not fit, a delete whose second step does not
// get through, and a split whose mark does not name a real stretch of the
// text. It runs against DirectoryObjectStore in a directory of its own, as
// docs/Konventionen.md, section Tests, requires, so what is checked here is
// the real file and not a substitute for one.

using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Documents;

/// <summary>Holds <see cref="DocumentService"/> to everything the task asks of it.</summary>
public sealed class DocumentServiceTests : IDisposable
{
    /// <summary>The moment a document is created in these tests.</summary>
    private static readonly DateTimeOffset Created = new(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The later moment at which it is changed.</summary>
    private static readonly DateTimeOffset Changed = new(2026, 9, 22, 8, 14, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "reviewmydoc-tests",
        Guid.NewGuid().ToString("n"));

    private readonly FixedTimeProvider _clock = new(Created);
    private readonly IObjectStore _objects;
    private readonly DocumentService _service;

    /// <summary>Builds a service over an empty directory of this test.</summary>
    public DocumentServiceTests()
    {
        _objects = new DirectoryObjectStore(_root);
        _service = new DocumentService(new DocumentStore(_objects), _clock);
    }

    private static CancellationToken Token => CancellationToken.None;

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

    [Fact]
    public async Task Adding_a_section_appends_it_with_an_empty_text()
    {
        var created = await CreateDocumentAsync("Gutachten");
        _clock.UtcNow = Changed;

        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));
        var withSecond = await SucceedsAsync(
            _service.AddSectionAsync(withSection.Document.Id, "Bewertung", withSection.ETag, Token));

        Assert.Equal(["Ausgangslage", "Bewertung"], withSecond.Document.Sections.Select(section => section.Heading));
        Assert.Equal([1, 2], withSecond.Document.Sections.Select(section => section.Order));
        Assert.Equal(string.Empty, await ReadSectionTextAsync(withSecond.Document, 1));
        await AssertNoEntryWithoutFileAsync(withSecond.Document);
    }

    // A heading with nothing in it is caught by the page, not by a message from
    // the service: docs/Konventionen.md gives binding and checking the input to
    // the Razor Page, and the service guards what would otherwise become a
    // nameless section in a file.
    [Fact]
    public async Task A_section_without_a_heading_is_refused()
    {
        var created = await CreateDocumentAsync("Gutachten");

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _service.AddSectionAsync(created.Document.Id, "   ", created.ETag, Token));
    }

    // One half of the promise of a stable identifier: the heading changes, the
    // identifier does not. Everything assigned to that section keeps pointing at
    // it.
    [Fact]
    public async Task Renaming_a_section_leaves_its_identifier_alone()
    {
        var document = await WithThreeSectionsAsync();
        var identifiers = Identifiers(document.Document);
        _clock.UtcNow = Changed;

        var renamed = await SucceedsAsync(_service.RenameSectionAsync(
            document.Document.Id,
            document.Document.Sections[1].Id,
            "Bewertung der Lage",
            document.ETag,
            Token));

        Assert.Equal(identifiers, Identifiers(renamed.Document));
        Assert.Equal("Bewertung der Lage", renamed.Document.Sections[1].Heading);
        Assert.Equal(Changed, renamed.Document.Sections[1].UpdatedAt);
        Assert.Equal(Created, renamed.Document.Sections[0].UpdatedAt);
    }

    [Fact]
    public async Task Renaming_a_section_that_does_not_belong_to_the_document_reports_it()
    {
        var document = await WithThreeSectionsAsync();
        var stranger = SectionIdentifier.Draw();

        var result = await _service.RenameSectionAsync(
            document.Document.Id,
            stranger,
            "Bewertung",
            document.ETag,
            Token);

        Assert.Equal(stranger, Assert.IsType<DocumentResult.SectionNotFound>(result).SectionId);
    }

    // The other half, and the one the whole story is named after: the order is
    // turned around and not a single identifier moves with it. Only the
    // positions, and with them the order field, change.
    [Fact]
    public async Task Reordering_leaves_every_identifier_alone()
    {
        var document = await WithThreeSectionsAsync();
        var identifiers = Identifiers(document.Document);
        var reversed = identifiers.Reverse().Select(value => new SectionIdentifier(value)).ToArray();
        _clock.UtcNow = Changed;

        var reordered = await SucceedsAsync(
            _service.ReorderSectionsAsync(document.Document.Id, reversed, document.ETag, Token));

        Assert.Equal(identifiers.Reverse(), Identifiers(reordered.Document));
        Assert.Equal(["Anlagen", "Bewertung", "Ausgangslage"], reordered.Document.Sections.Select(section => section.Heading));
        Assert.Equal([1, 2, 3], reordered.Document.Sections.Select(section => section.Order));

        // Being moved is not a change to the section itself; the document alone
        // records the move.
        Assert.All(reordered.Document.Sections, section => Assert.Equal(Created, section.UpdatedAt));
        Assert.Equal(Changed, reordered.Document.UpdatedAt);
    }

    [Fact]
    public async Task Reordering_survives_being_read_back()
    {
        var document = await WithThreeSectionsAsync();
        var reversed = Identifiers(document.Document).Reverse().Select(value => new SectionIdentifier(value)).ToArray();
        var reordered = await SucceedsAsync(
            _service.ReorderSectionsAsync(document.Document.Id, reversed, document.ETag, Token));

        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(document.Document.Id, Token));

        Assert.Equal(Identifiers(reordered.Document), Identifiers(loaded.Document));
        Assert.Equal([1, 2, 3], loaded.Document.Sections.Select(section => section.Order));
    }

    [Fact]
    public async Task Reordering_with_an_identifier_that_names_no_section_reports_it()
    {
        var document = await WithThreeSectionsAsync();
        var stranger = SectionIdentifier.Draw();
        var order = document.Document.Sections.Select(section => section.Id).Take(2).Append(stranger).ToArray();

        var result = await _service.ReorderSectionsAsync(document.Document.Id, order, document.ETag, Token);

        Assert.Equal(stranger, Assert.IsType<DocumentResult.SectionNotFound>(result).SectionId);
    }

    // A list that leaves a section out would quietly drop it from the outline
    // while its text stayed on disk, which is exactly the state this aggregate
    // must never reach.
    [Fact]
    public async Task Reordering_that_leaves_a_section_out_is_refused()
    {
        var document = await WithThreeSectionsAsync();
        var order = document.Document.Sections.Select(section => section.Id).Take(2).ToArray();

        var result = await _service.ReorderSectionsAsync(document.Document.Id, order, document.ETag, Token);

        Assert.IsType<DocumentResult.OrderDoesNotMatchSections>(result);
        await AssertUnchangedAsync(document);
    }

    [Fact]
    public async Task Reordering_that_names_a_section_twice_is_refused()
    {
        var document = await WithThreeSectionsAsync();
        var first = document.Document.Sections[0].Id;
        var order = new[] { first, first, document.Document.Sections[1].Id };

        var result = await _service.ReorderSectionsAsync(document.Document.Id, order, document.ETag, Token);

        Assert.IsType<DocumentResult.OrderDoesNotMatchSections>(result);
        await AssertUnchangedAsync(document);
    }

    // The delete the task asks about: entry and file go together, and the
    // identifiers of the sections that stay are untouched even though their
    // positions move up.
    [Fact]
    public async Task Deleting_a_section_removes_its_entry_and_its_file()
    {
        var document = await WithThreeSectionsAsync();
        var removed = document.Document.Sections[1].Id;
        var surviving = new[] { document.Document.Sections[0].Id.Value, document.Document.Sections[2].Id.Value };
        _clock.UtcNow = Changed;

        var afterwards = await SucceedsAsync(
            _service.DeleteSectionAsync(document.Document.Id, removed, document.ETag, Token));

        Assert.Equal(surviving, Identifiers(afterwards.Document));
        Assert.Equal(["Ausgangslage", "Anlagen"], afterwards.Document.Sections.Select(section => section.Heading));
        Assert.Equal([1, 2], afterwards.Document.Sections.Select(section => section.Order));
        Assert.DoesNotContain(
            $"documents/{document.Document.Id}/sections/{removed}.md",
            await _objects.ListAsync($"documents/{document.Document.Id}/sections/", Token));
        await AssertNoEntryWithoutFileAsync(afterwards.Document);
    }

    // The point of the order the service writes in. The cleanup of the text
    // fails, so the change is left half done in the only way it can be - and the
    // half that survives is the one the document can live with: the entry is
    // gone, the file is the leftover. The opposite would leave a section in the
    // outline whose text nobody could open.
    [Fact]
    public async Task A_failing_cleanup_leaves_a_file_without_an_entry_and_never_the_other_way_round()
    {
        var document = await WithThreeSectionsAsync();
        var removed = document.Document.Sections[1].Id;
        var failing = new FailingObjectStore(_objects) { FailsToDelete = true };
        var service = new DocumentService(new DocumentStore(failing), _clock);

        await Assert.ThrowsAsync<ObjectStoreException>(
            () => service.DeleteSectionAsync(document.Document.Id, removed, document.ETag, Token));

        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(document.Document.Id, Token));
        Assert.Null(loaded.Document.FindSection(removed));
        await AssertNoEntryWithoutFileAsync(loaded.Document);
        Assert.Contains(
            $"documents/{document.Document.Id}/sections/{removed}.md",
            await _objects.ListAsync($"documents/{document.Document.Id}/sections/", Token));
    }

    // Deleting the same section again after such a failure has to stay harmless:
    // the entry is already gone, so the answer is that there is no such section,
    // and the leftover file can still be cleared away.
    [Fact]
    public async Task Deleting_a_section_that_does_not_belong_to_the_document_reports_it()
    {
        var document = await WithThreeSectionsAsync();
        var stranger = SectionIdentifier.Draw();

        var result = await _service.DeleteSectionAsync(document.Document.Id, stranger, document.ETag, Token);

        Assert.Equal(stranger, Assert.IsType<DocumentResult.SectionNotFound>(result).SectionId);
        await AssertUnchangedAsync(document);
    }

    // The heart of the task: the identifier of the section being split stays
    // with what stood before the mark, the mark gets its own fresh identifier
    // and its new heading, and what stood after the mark keeps the original
    // heading under a fresh identifier of its own.
    [Fact]
    public async Task Splitting_in_the_middle_produces_three_parts_and_keeps_the_identifier_before_the_mark()
    {
        var created = await CreateDocumentAsync("Gutachten");
        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));
        var sectionId = withSection.Document.Sections[0].Id;
        var text = "Vor der Markierung. Die Markierung selbst. Nach der Markierung.";
        await WriteSectionTextAsync(withSection.Document.Id, sectionId, text);
        var markStart = text.IndexOf("Die Markierung selbst.", StringComparison.Ordinal);
        var markEnd = markStart + "Die Markierung selbst.".Length;
        _clock.UtcNow = Changed;

        var split = await SucceedsAsync(_service.SplitSectionAsync(
            withSection.Document.Id, sectionId, markStart, markEnd, "Markierter Abschnitt", withSection.ETag, Token));

        Assert.Equal(3, split.Document.Sections.Count);
        var before = split.Document.Sections[0];
        var marked = split.Document.Sections[1];
        var after = split.Document.Sections[2];
        Assert.Equal(sectionId, before.Id);
        Assert.Equal("Ausgangslage", before.Heading);
        Assert.NotEqual(sectionId, marked.Id);
        Assert.Equal("Markierter Abschnitt", marked.Heading);
        Assert.NotEqual(sectionId, after.Id);
        Assert.NotEqual(marked.Id, after.Id);
        Assert.Equal("Ausgangslage", after.Heading);
        Assert.Equal([1, 2, 3], split.Document.Sections.Select(section => section.Order));
        Assert.All(split.Document.Sections, section => Assert.Equal(Changed, section.UpdatedAt));

        Assert.Equal("Vor der Markierung. ", await ReadEntryAsync($"documents/{split.Document.Id}/sections/{before.Id}.md"));
        Assert.Equal("Die Markierung selbst.", await ReadEntryAsync($"documents/{split.Document.Id}/sections/{marked.Id}.md"));
        Assert.Equal(" Nach der Markierung.", await ReadEntryAsync($"documents/{split.Document.Id}/sections/{after.Id}.md"));
        await AssertNoEntryWithoutFileAsync(split.Document);
    }

    // The mark opens the section, so there is no text before it. The identifier
    // goes to the part that holds the first character of the original text,
    // which is now the mark itself. It never ends here: a piece of feedback
    // pointing at it would otherwise lead nowhere while the prose it was written
    // about is still in the document, only under another name.
    [Fact]
    public async Task Splitting_at_the_start_of_the_section_leaves_the_identifier_on_the_mark()
    {
        var created = await CreateDocumentAsync("Gutachten");
        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));
        var sectionId = withSection.Document.Sections[0].Id;
        var text = "Die Markierung selbst. Nach der Markierung.";
        await WriteSectionTextAsync(withSection.Document.Id, sectionId, text);
        var markEnd = "Die Markierung selbst.".Length;

        var split = await SucceedsAsync(_service.SplitSectionAsync(
            withSection.Document.Id, sectionId, 0, markEnd, "Markierter Abschnitt", withSection.ETag, Token));

        Assert.Equal(2, split.Document.Sections.Count);
        var marked = split.Document.Sections[0];
        var after = split.Document.Sections[1];
        Assert.Equal(sectionId, marked.Id);
        Assert.NotEqual(sectionId, after.Id);
        Assert.Equal("Markierter Abschnitt", marked.Heading);
        Assert.Equal("Ausgangslage", after.Heading);
        Assert.Equal("Die Markierung selbst.", await ReadEntryAsync($"documents/{split.Document.Id}/sections/{sectionId}.md"));
        Assert.Equal(" Nach der Markierung.", await ReadEntryAsync($"documents/{split.Document.Id}/sections/{after.Id}.md"));
        await AssertNoEntryWithoutFileAsync(split.Document);
    }

    // Whichever way the mark falls, the identifier survives the split. Without
    // that, a review order or a piece of feedback would point at a section that
    // is no longer there while its text still is.
    [Theory]
    [InlineData(0, 22)]
    [InlineData(4, 22)]
    [InlineData(4, 43)]
    public async Task Splitting_never_makes_the_identifier_disappear(int markStart, int markEnd)
    {
        var created = await CreateDocumentAsync("Gutachten");
        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));
        var sectionId = withSection.Document.Sections[0].Id;
        await WriteSectionTextAsync(
            withSection.Document.Id, sectionId, "Die Markierung selbst. Nach der Markierung.");

        var split = await SucceedsAsync(_service.SplitSectionAsync(
            withSection.Document.Id, sectionId, markStart, markEnd, "Markierter Abschnitt", withSection.ETag, Token));

        Assert.NotNull(split.Document.FindSection(sectionId));
        await AssertNoEntryWithoutFileAsync(split.Document);
    }

    // The mirror image: the mark reaches to the very end, so there is nothing
    // after it and the identifier stays where docs/Konzept.md always puts it,
    // on the text before the mark.
    [Fact]
    public async Task Splitting_at_the_end_of_the_section_produces_two_parts_and_keeps_the_identifier_on_the_first()
    {
        var created = await CreateDocumentAsync("Gutachten");
        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));
        var sectionId = withSection.Document.Sections[0].Id;
        var text = "Vor der Markierung. Die Markierung selbst.";
        await WriteSectionTextAsync(withSection.Document.Id, sectionId, text);
        var markStart = text.IndexOf("Die Markierung selbst.", StringComparison.Ordinal);

        var split = await SucceedsAsync(_service.SplitSectionAsync(
            withSection.Document.Id, sectionId, markStart, text.Length, "Markierter Abschnitt", withSection.ETag, Token));

        Assert.Equal(2, split.Document.Sections.Count);
        var before = split.Document.Sections[0];
        var marked = split.Document.Sections[1];
        Assert.Equal(sectionId, before.Id);
        Assert.Equal("Ausgangslage", before.Heading);
        Assert.NotEqual(sectionId, marked.Id);
        Assert.Equal("Markierter Abschnitt", marked.Heading);
        Assert.Equal("Vor der Markierung. ", await ReadEntryAsync($"documents/{split.Document.Id}/sections/{before.Id}.md"));
        Assert.Equal("Die Markierung selbst.", await ReadEntryAsync($"documents/{split.Document.Id}/sections/{marked.Id}.md"));
    }

    // The other random case: the mark covers the whole section, so nothing
    // actually splits off and this behaves exactly like a rename - same
    // identifier, same text, only the heading is the argument's.
    [Fact]
    public async Task Splitting_the_whole_section_only_changes_the_heading()
    {
        var created = await CreateDocumentAsync("Gutachten");
        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));
        var sectionId = withSection.Document.Sections[0].Id;
        var text = "Der ganze Abschnitt ist markiert.";
        await WriteSectionTextAsync(withSection.Document.Id, sectionId, text);
        _clock.UtcNow = Changed;

        var split = await SucceedsAsync(_service.SplitSectionAsync(
            withSection.Document.Id, sectionId, 0, text.Length, "Neue Überschrift", withSection.ETag, Token));

        var only = Assert.Single(split.Document.Sections);
        Assert.Equal(sectionId, only.Id);
        Assert.Equal("Neue Überschrift", only.Heading);
        Assert.Equal(Changed, only.UpdatedAt);
        Assert.Equal(text, await ReadEntryAsync($"documents/{split.Document.Id}/sections/{sectionId}.md"));
    }

    // Wherever the mark falls, the parts add back up to the original text -
    // splitting only draws boundaries, it never drops or duplicates a character.
    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 7)]
    [InlineData(7, 10)]
    [InlineData(0, 10)]
    public async Task Splitting_reproduces_the_original_text_when_the_parts_are_joined(int markStart, int markEnd)
    {
        const string text = "0123456789";
        var created = await CreateDocumentAsync("Gutachten");
        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));
        var sectionId = withSection.Document.Sections[0].Id;
        await WriteSectionTextAsync(withSection.Document.Id, sectionId, text);

        var split = await SucceedsAsync(_service.SplitSectionAsync(
            withSection.Document.Id, sectionId, markStart, markEnd, "Markiert", withSection.ETag, Token));

        var parts = await Task.WhenAll(split.Document.Sections.Select(
            section => ReadEntryAsync($"documents/{split.Document.Id}/sections/{section.Id}.md")));
        Assert.Equal(text, string.Concat(parts));
    }

    // The other three sections do not move, and the parts of the split one take
    // exactly the place it stood in, not the end of the list.
    [Fact]
    public async Task Splitting_puts_the_new_parts_exactly_where_the_original_section_stood()
    {
        var document = await WithThreeSectionsAsync();
        var first = document.Document.Sections[0];
        var middle = document.Document.Sections[1];
        var last = document.Document.Sections[2];
        var text = "Vor der Markierung. Die Markierung selbst. Nach der Markierung.";
        await WriteSectionTextAsync(document.Document.Id, middle.Id, text);
        var markStart = text.IndexOf("Die Markierung selbst.", StringComparison.Ordinal);
        var markEnd = markStart + "Die Markierung selbst.".Length;

        var split = await SucceedsAsync(_service.SplitSectionAsync(
            document.Document.Id, middle.Id, markStart, markEnd, "Neuer Abschnitt", document.ETag, Token));

        Assert.Equal(5, split.Document.Sections.Count);
        Assert.Equal(first.Id, split.Document.Sections[0].Id);
        Assert.Equal(middle.Id, split.Document.Sections[1].Id);
        Assert.Equal("Neuer Abschnitt", split.Document.Sections[2].Heading);
        Assert.Equal("Bewertung", split.Document.Sections[3].Heading);
        Assert.Equal(last.Id, split.Document.Sections[4].Id);
        Assert.Equal([1, 2, 3, 4, 5], split.Document.Sections.Select(section => section.Order));
        Assert.Equal(first.UpdatedAt, split.Document.Sections[0].UpdatedAt);
        Assert.Equal(last.UpdatedAt, split.Document.Sections[4].UpdatedAt);
        await AssertNoEntryWithoutFileAsync(split.Document);
    }

    // A start equal to the end names no text at all, and the task calls for
    // this to be refused as a value beside the form, never as an exception.
    [Fact]
    public async Task Splitting_with_an_empty_selection_is_rejected_as_a_value()
    {
        var document = await WithThreeSectionsAsync();
        var sectionId = document.Document.Sections[1].Id;
        await WriteSectionTextAsync(document.Document.Id, sectionId, "Ein Text.");

        var result = await _service.SplitSectionAsync(document.Document.Id, sectionId, 3, 3, "Neu", document.ETag, Token);

        Assert.IsType<DocumentResult.InvalidSelection>(result);
        await AssertUnchangedAsync(document);
    }

    // A selection reaching outside the text, or an end before its start, is the
    // same kind of stale input as an empty one and is refused the same way.
    [Theory]
    [InlineData(-1, 5)]
    [InlineData(0, 100)]
    [InlineData(5, 2)]
    public async Task Splitting_outside_the_text_or_backwards_is_rejected_as_a_value(int markStart, int markEnd)
    {
        var document = await WithThreeSectionsAsync();
        var sectionId = document.Document.Sections[1].Id;
        await WriteSectionTextAsync(document.Document.Id, sectionId, "Ein kurzer Text.");

        var result = await _service.SplitSectionAsync(
            document.Document.Id, sectionId, markStart, markEnd, "Neu", document.ETag, Token);

        Assert.IsType<DocumentResult.InvalidSelection>(result);
        await AssertUnchangedAsync(document);
    }

    [Fact]
    public async Task Splitting_a_section_that_does_not_belong_to_the_document_reports_it()
    {
        var document = await WithThreeSectionsAsync();
        var stranger = SectionIdentifier.Draw();

        var result = await _service.SplitSectionAsync(document.Document.Id, stranger, 0, 1, "Neu", document.ETag, Token);

        Assert.Equal(stranger, Assert.IsType<DocumentResult.SectionNotFound>(result).SectionId);
    }

    [Fact]
    public async Task Splitting_a_section_of_a_document_that_is_not_there_reports_it()
    {
        var result = await _service.SplitSectionAsync(
            DocumentIdentifier.Draw(), SectionIdentifier.Draw(), 0, 1, "Neu", new ETag("irrelevant"), Token);

        Assert.IsType<DocumentResult.DocumentNotFound>(result);
    }

    [Fact]
    public async Task Splitting_with_an_outdated_version_is_a_conflict()
    {
        var (document, outdated) = await OvertakenAsync();
        var sectionId = document.Document.Sections[0].Id;

        var result = await _service.SplitSectionAsync(document.Document.Id, sectionId, 0, 1, "Neu", outdated, Token);

        Assert.IsType<DocumentResult.Conflict>(result);
        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(document.Document.Id, Token));
        Assert.NotNull(loaded.Document.FindSection(sectionId));
        Assert.Equal(3, loaded.Document.Sections.Count);
    }

    // The compensation the order of the writes exists for, the same way
    // Adding_a_section_with_an_outdated_version_is_a_conflict_and_leaves_no_text_behind
    // holds AddSectionAsync to it: the two freshly written texts are removed
    // again once document.json refuses the write, so a refused split leaves
    // exactly the files it found.
    [Fact]
    public async Task Splitting_with_an_outdated_version_leaves_no_new_text_behind()
    {
        var (document, outdated) = await OvertakenAsync();
        var sectionId = document.Document.Sections[0].Id;
        var text = "Vorher. Nachher.";
        await WriteSectionTextAsync(document.Document.Id, sectionId, text);
        var markStart = text.IndexOf("Nachher.", StringComparison.Ordinal);
        var before = await _objects.ListAsync($"documents/{document.Document.Id}/sections/", Token);

        var result = await _service.SplitSectionAsync(
            document.Document.Id, sectionId, markStart, text.Length, "Neu", outdated, Token);

        Assert.IsType<DocumentResult.Conflict>(result);
        Assert.Equal(before, await _objects.ListAsync($"documents/{document.Document.Id}/sections/", Token));
    }

    // Assurance 1 of docs/Datenmodell.md as the user meets it, for every
    // operation that writes: the version handed back is no longer the current
    // one, so the change is refused as a value - never as an exception, because
    // the page has to show it as a sentence beside the form - and nothing is
    // written.
    [Fact]
    public async Task Renaming_a_document_with_an_outdated_version_is_a_conflict()
    {
        var (document, outdated) = await OvertakenAsync();

        var result = await _service.RenameDocumentAsync(document.Document.Id, "Anderer Titel", outdated, Token);

        Assert.IsType<DocumentResult.Conflict>(result);
        Assert.Equal("Zwischendurch umbenannt", (await SucceedsAsync(
            _service.LoadDocumentAsync(document.Document.Id, Token))).Document.Title);
    }

    [Fact]
    public async Task Renaming_a_section_with_an_outdated_version_is_a_conflict()
    {
        var (document, outdated) = await OvertakenAsync();

        var result = await _service.RenameSectionAsync(
            document.Document.Id,
            document.Document.Sections[0].Id,
            "Andere Überschrift",
            outdated,
            Token);

        Assert.IsType<DocumentResult.Conflict>(result);
        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(document.Document.Id, Token));
        Assert.Equal("Ausgangslage", loaded.Document.Sections[0].Heading);
    }

    [Fact]
    public async Task Reordering_with_an_outdated_version_is_a_conflict()
    {
        var (document, outdated) = await OvertakenAsync();
        var reversed = Identifiers(document.Document).Reverse().Select(value => new SectionIdentifier(value)).ToArray();

        var result = await _service.ReorderSectionsAsync(document.Document.Id, reversed, outdated, Token);

        Assert.IsType<DocumentResult.Conflict>(result);
        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(document.Document.Id, Token));
        Assert.Equal(Identifiers(document.Document), Identifiers(loaded.Document));
    }

    // The conflict has to leave the text alone as well: a section that is still
    // in the outline whose file had already been cleared away would be the very
    // damage the order of the writes is there to prevent.
    [Fact]
    public async Task Deleting_a_section_with_an_outdated_version_is_a_conflict_and_keeps_the_text()
    {
        var (document, outdated) = await OvertakenAsync();
        var section = document.Document.Sections[0].Id;

        var result = await _service.DeleteSectionAsync(document.Document.Id, section, outdated, Token);

        Assert.IsType<DocumentResult.Conflict>(result);
        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(document.Document.Id, Token));
        Assert.NotNull(loaded.Document.FindSection(section));
        await AssertNoEntryWithoutFileAsync(loaded.Document);
    }

    // Adding a section writes the text first, so a conflict on the outline
    // leaves a text nothing points at. It is cleared away again, which this test
    // holds the service to: otherwise every refused attempt would leave a file
    // behind.
    [Fact]
    public async Task Adding_a_section_with_an_outdated_version_is_a_conflict_and_leaves_no_text_behind()
    {
        var (document, outdated) = await OvertakenAsync();
        var before = await _objects.ListAsync($"documents/{document.Document.Id}/sections/", Token);

        var result = await _service.AddSectionAsync(document.Document.Id, "Bewertung", outdated, Token);

        Assert.IsType<DocumentResult.Conflict>(result);
        Assert.Equal(before, await _objects.ListAsync($"documents/{document.Document.Id}/sections/", Token));
    }

    // The heart of the task: a frozen state carries the outline AND the text of
    // every section, spelled exactly as docs/Datenmodell.md shows it. Compared as
    // text for the same reason as document.json above - the file is the promise.
    [Fact]
    public async Task Freezing_writes_the_outline_and_every_section_text()
    {
        var created = await CreateDocumentAsync("Gutachten Musterstraße");
        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));
        var sectionId = withSection.Document.Sections[0].Id;
        await WriteSectionTextAsync(withSection.Document.Id, sectionId, "Der Auftraggeber hat 2024 ein Grundstück erworben.");
        _clock.UtcNow = Changed;

        var frozen = await SucceedsAsync(_service.FreezeVersionAsync(withSection.Document.Id, withSection.ETag, Token));

        var documentId = frozen.Document.Id.Value;
        var content = await ReadEntryAsync($"documents/{documentId}/versions/1.json");
        Assert.Equal(
            $$"""
            {
              "documentId": "{{documentId}}",
              "version": 1,
              "title": "Gutachten Musterstraße",
              "sections": [
                {
                  "id": "{{sectionId}}",
                  "heading": "Ausgangslage",
                  "order": 1,
                  "text": "Der Auftraggeber hat 2024 ein Grundstück erworben."
                }
              ],
              "frozenAt": "2026-09-22T08:14:00Z"
            }
            """,
            content);
    }

    // The version count is the point of the whole operation: it climbs by one and
    // the new number is the one document.json now carries, not a separate counter
    // that could disagree with it.
    [Fact]
    public async Task Freezing_counts_up_the_version_and_records_it_in_the_document()
    {
        var document = await WithThreeSectionsAsync();
        _clock.UtcNow = Changed;

        var frozen = await SucceedsAsync(_service.FreezeVersionAsync(document.Document.Id, document.ETag, Token));

        Assert.Equal(1, frozen.Document.Version);
        Assert.Equal(Changed, frozen.Document.UpdatedAt);
        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(document.Document.Id, Token));
        Assert.Equal(1, loaded.Document.Version);

        var secondFreeze = await SucceedsAsync(_service.FreezeVersionAsync(loaded.Document.Id, loaded.ETag, Token));
        Assert.Equal(2, secondFreeze.Document.Version);
    }

    [Fact]
    public async Task Freezing_a_document_that_is_not_there_reports_it()
    {
        var result = await _service.FreezeVersionAsync(DocumentIdentifier.Draw(), new ETag("irrelevant"), Token);

        Assert.IsType<DocumentResult.DocumentNotFound>(result);
    }

    [Fact]
    public async Task Freezing_with_an_outdated_version_is_a_conflict()
    {
        var (document, outdated) = await OvertakenAsync();

        var result = await _service.FreezeVersionAsync(document.Document.Id, outdated, Token);

        Assert.IsType<DocumentResult.Conflict>(result);
        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(document.Document.Id, Token));
        Assert.Equal(0, loaded.Document.Version);
    }

    // The compensation the order of the two writes exists for: the frozen state
    // is written first, so a conflict on document.json has to remove it again -
    // otherwise the same version number would look taken to every later attempt,
    // and freezing this document would never succeed again. See the remarks of
    // FreezeVersionAsync for the one case this cannot reach.
    [Fact]
    public async Task Freezing_with_an_outdated_version_is_a_conflict_and_leaves_no_version_file_behind()
    {
        var (document, outdated) = await OvertakenAsync();

        var result = await _service.FreezeVersionAsync(document.Document.Id, outdated, Token);

        Assert.IsType<DocumentResult.Conflict>(result);
        Assert.Empty(await _objects.ListAsync($"documents/{document.Document.Id}/versions/", Token));
    }

    // Assurance 2 of docs/Datenmodell.md, tested where it is actually kept: the
    // object store refuses a second write to an existing version instead of
    // overwriting it, and the first content survives untouched.
    [Fact]
    public async Task A_second_freeze_of_the_same_version_number_is_refused_not_overwritten()
    {
        var document = await WithThreeSectionsAsync();
        var frozen = await SucceedsAsync(_service.FreezeVersionAsync(document.Document.Id, document.ETag, Token));
        var before = await ReadEntryAsync($"documents/{document.Document.Id}/versions/1.json");

        var repeated = new DocumentVersion(
            frozen.Document.Id,
            version: 1,
            title: "Ein anderer Titel, der nie ankommen darf",
            sections: [],
            frozenAt: Changed);
        var store = new DocumentStore(_objects);
        var result = await store.WriteVersionAsync(repeated, WriteCondition.MustNotExist, Token);

        Assert.IsType<ObjectWriteResult.Conflict>(result);
        Assert.Equal(before, await ReadEntryAsync($"documents/{document.Document.Id}/versions/1.json"));
    }

    // A document straight from the page that creates one has no section yet, and
    // the owner may well send it to review before writing the first one. An empty
    // outline is a state and not a missing one, so it freezes like any other.
    [Fact]
    public async Task Freezing_a_document_without_a_section_freezes_an_empty_outline()
    {
        var created = await CreateDocumentAsync("Noch ohne Abschnitte");
        _clock.UtcNow = Changed;

        var frozen = await SucceedsAsync(_service.FreezeVersionAsync(created.Document.Id, created.ETag, Token));

        var documentId = frozen.Document.Id.Value;
        var content = await ReadEntryAsync($"documents/{documentId}/versions/1.json");
        Assert.Equal(
            $$"""
            {
              "documentId": "{{documentId}}",
              "version": 1,
              "title": "Noch ohne Abschnitte",
              "sections": [],
              "frozenAt": "2026-09-22T08:14:00Z"
            }
            """,
            content);
    }

    // A section that was added but never written carries an empty text, and empty
    // is a text like any other. Freezing it has to keep it as it is; reading it
    // as "nothing there" would make the freeze fail on every fresh section.
    [Fact]
    public async Task Freezing_keeps_the_empty_text_of_a_section_nobody_has_written_yet()
    {
        var created = await CreateDocumentAsync("Gliederung steht, Text fehlt");
        var withSection = await SucceedsAsync(
            _service.AddSectionAsync(created.Document.Id, "Ausgangslage", created.ETag, Token));

        var frozen = await SucceedsAsync(_service.FreezeVersionAsync(withSection.Document.Id, withSection.ETag, Token));

        var content = await ReadEntryAsync($"documents/{frozen.Document.Id}/versions/1.json");
        Assert.Contains("\"text\": \"\"", content, StringComparison.Ordinal);

        // And the comparison agrees: an empty text that has not been touched is
        // not a difference.
        var comparison = await _service.FindChangedSectionsAsync(frozen.Document.Id, 1, Token);
        Assert.Empty(Assert.IsType<VersionComparisonResult.Success>(comparison).ChangedSectionIds);
    }

    // The comparison the review view and the editor will both read from: right
    // after freezing, today's text agrees everywhere with what was just frozen.
    [Fact]
    public async Task Comparing_right_after_freezing_finds_no_difference()
    {
        var document = await WithThreeSectionsAsync();
        var frozen = await SucceedsAsync(_service.FreezeVersionAsync(document.Document.Id, document.ETag, Token));

        var comparison = await _service.FindChangedSectionsAsync(frozen.Document.Id, 1, Token);

        var success = Assert.IsType<VersionComparisonResult.Success>(comparison);
        Assert.Empty(success.ChangedSectionIds);
    }

    // The case docs/Konzept.md asks the interface to show on both sides: the
    // owner keeps writing after sending a section out, and the comparison has to
    // notice it without anybody telling it which section changed.
    [Fact]
    public async Task Comparing_after_editing_a_frozen_section_finds_it_changed()
    {
        var document = await WithThreeSectionsAsync();
        var frozen = await SucceedsAsync(_service.FreezeVersionAsync(document.Document.Id, document.ETag, Token));
        var editedSection = frozen.Document.Sections[1].Id;
        var untouchedSection = frozen.Document.Sections[0].Id;
        await WriteSectionTextAsync(frozen.Document.Id, editedSection, "Neuer Text nach dem Versenden.");

        var comparison = await _service.FindChangedSectionsAsync(frozen.Document.Id, 1, Token);

        var success = Assert.IsType<VersionComparisonResult.Success>(comparison);
        Assert.Equal([editedSection], success.ChangedSectionIds);
        Assert.DoesNotContain(untouchedSection, success.ChangedSectionIds);
    }

    // A section that did not exist yet when the state was frozen is content the
    // reviewer never saw, so it counts as changed exactly like an edited one.
    [Fact]
    public async Task Comparing_finds_a_section_added_after_freezing()
    {
        var document = await WithThreeSectionsAsync();
        var frozen = await SucceedsAsync(_service.FreezeVersionAsync(document.Document.Id, document.ETag, Token));
        var withNewSection = await SucceedsAsync(
            _service.AddSectionAsync(frozen.Document.Id, "Neu hinzugefügt", frozen.ETag, Token));
        var newSection = withNewSection.Document.Sections[^1].Id;

        var comparison = await _service.FindChangedSectionsAsync(frozen.Document.Id, 1, Token);

        var success = Assert.IsType<VersionComparisonResult.Success>(comparison);
        Assert.Equal([newSection], success.ChangedSectionIds);
    }

    [Fact]
    public async Task Comparing_a_document_that_is_not_there_reports_it()
    {
        var result = await _service.FindChangedSectionsAsync(DocumentIdentifier.Draw(), 1, Token);

        Assert.IsType<VersionComparisonResult.DocumentNotFound>(result);
    }

    [Fact]
    public async Task Comparing_against_a_version_that_was_never_frozen_reports_it()
    {
        var document = await WithThreeSectionsAsync();

        var result = await _service.FindChangedSectionsAsync(document.Document.Id, 1, Token);

        Assert.IsType<VersionComparisonResult.VersionNotFound>(result);
    }

    /// <summary>Removes the directory of this test.</summary>
    /// <remarks>
    /// A directory that cannot be removed does not turn a green test red: the
    /// test has then already shown what it had to show.
    /// </remarks>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Creates a document of the one owner this application has.</summary>
    private async Task<DocumentResult.Success> CreateDocumentAsync(string title) =>
        await SucceedsAsync(_service.CreateDocumentAsync("owner", title, Token));

    /// <summary>Creates a document with three sections, all stamped at <see cref="Created"/>.</summary>
    private async Task<DocumentResult.Success> WithThreeSectionsAsync()
    {
        var document = await CreateDocumentAsync("Gutachten");
        foreach (var heading in new[] { "Ausgangslage", "Bewertung", "Anlagen" })
        {
            document = await SucceedsAsync(
                _service.AddSectionAsync(document.Document.Id, heading, document.ETag, Token));
        }

        return document;
    }

    /// <summary>
    /// Builds a document that somebody else has changed in the meantime, and
    /// hands back the version the first caller still holds.
    /// </summary>
    private async Task<(DocumentResult.Success Document, ETag Outdated)> OvertakenAsync()
    {
        var document = await WithThreeSectionsAsync();
        var outdated = document.ETag;
        var overtaken = await SucceedsAsync(
            _service.RenameDocumentAsync(document.Document.Id, "Zwischendurch umbenannt", outdated, Token));

        Assert.NotEqual(outdated, overtaken.ETag);

        return (document, outdated);
    }

    /// <summary>Insists that an operation took effect and hands back its result.</summary>
    private static async Task<DocumentResult.Success> SucceedsAsync(Task<DocumentResult> operation) =>
        Assert.IsType<DocumentResult.Success>(await operation);

    /// <summary>The identifiers of the sections, in the order the outline holds them.</summary>
    private static string[] Identifiers(Document document) =>
        [.. document.Sections.Select(section => section.Id.Value)];

    /// <summary>Reads one entry of the store, insisting that it is there.</summary>
    private async Task<string> ReadEntryAsync(string path) =>
        Assert.IsType<ObjectReadResult.Found>(await _objects.ReadAsync(path, Token)).Content;

    /// <summary>Reads the Markdown text of the section at a position of the outline.</summary>
    private async Task<string> ReadSectionTextAsync(Document document, int position) =>
        await ReadEntryAsync($"documents/{document.Id}/sections/{document.Sections[position].Id}.md");

    /// <summary>
    /// Puts a section's text directly into the store, bypassing the service - the
    /// section editor of a later story is what would normally do this.
    /// </summary>
    private async Task WriteSectionTextAsync(DocumentIdentifier documentId, SectionIdentifier sectionId, string text) =>
        Assert.IsType<ObjectWriteResult.Written>(await _objects.WriteAsync(
            $"documents/{documentId}/sections/{sectionId}.md",
            text,
            WriteCondition.Unconditional,
            Token));

    /// <summary>
    /// Insists that every section of the outline has its text beside it.
    /// </summary>
    /// <remarks>
    /// This is the state the aggregate must never leave: an entry without a file
    /// is a section that cannot be opened, while a file without an entry is only
    /// an untidy leftover.
    /// </remarks>
    private async Task AssertNoEntryWithoutFileAsync(Document document)
    {
        var texts = await _objects.ListAsync($"documents/{document.Id}/sections/", Token);

        foreach (var section in document.Sections)
        {
            Assert.Contains($"documents/{document.Id}/sections/{section.Id}.md", texts);
        }
    }

    /// <summary>Insists that a refused operation changed nothing on disk.</summary>
    private async Task AssertUnchangedAsync(DocumentResult.Success document)
    {
        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(document.Document.Id, Token));

        Assert.Equal(document.ETag, loaded.ETag);
        Assert.Equal(Identifiers(document.Document), Identifiers(loaded.Document));
    }
}
