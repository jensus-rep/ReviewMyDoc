// Checks that stale document operations leave stored content unchanged.

using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Documents;

/// <summary>Checks that stale document operations leave stored content unchanged.</summary>
public sealed class DocumentConcurrencyTests : DocumentServiceTestBase
{
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
}
