// Checks immutable snapshots and comparisons against the current document.

using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Documents;

/// <summary>Checks immutable snapshots and comparisons against the current document.</summary>
public sealed class DocumentVersionServiceTests : DocumentServiceTestBase
{
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
}
