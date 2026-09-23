// Checks section splitting, selection boundaries and text preservation.

using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Documents;

/// <summary>Checks section splitting, selection boundaries and text preservation.</summary>
public sealed class DocumentSplitServiceTests : DocumentServiceTestBase
{
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
}
