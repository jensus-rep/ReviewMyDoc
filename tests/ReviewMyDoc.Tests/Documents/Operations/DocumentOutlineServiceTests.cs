// Checks outline changes and cleanup against real storage.

using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Documents;

/// <summary>Checks outline changes and cleanup against real storage.</summary>
public sealed class DocumentOutlineServiceTests : DocumentServiceTestBase
{
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
}
