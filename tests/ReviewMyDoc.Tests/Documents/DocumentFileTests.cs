// What happens when a document.json is read that this application did not write
// the way it writes them: a field is missing, a value is not one of the model's,
// or somebody moved a section by hand. Checked through the service and a raw
// entry in the store, because that is the path a real file takes.

using System.Text.Json;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Documents;

/// <summary>Holds the reading of <c>document.json</c> to the model.</summary>
public sealed class DocumentFileTests : IDisposable
{
    private const string DocumentId = "d7kq2fr";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "reviewmydoc-tests",
        Guid.NewGuid().ToString("n"));

    private readonly IObjectStore _objects;
    private readonly DocumentService _service;

    /// <summary>Builds a service over an empty directory of this test.</summary>
    public DocumentFileTests()
    {
        _objects = new DirectoryObjectStore(_root);
        _service = new DocumentService(
            new DocumentStore(_objects),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 22, 8, 14, 0, TimeSpan.Zero)));
    }

    private static CancellationToken Token => CancellationToken.None;

    // A document written the way docs/Datenmodell.md shows it is read back
    // complete, including the state and the version it carries. Those two are
    // carried here and not changed - the transitions belong to the later epics -
    // and a document that sits in review has to survive being loaded and saved
    // by this service all the same.
    [Fact]
    public async Task A_file_of_the_model_is_read_with_everything_in_it()
    {
        await WriteFileAsync("""
            {
              "id": "d7kq2fr",
              "ownerId": "owner",
              "title": "Gutachten Musterstraße",
              "state": "InReview",
              "version": 4,
              "sections": [
                { "id": "s_1a2b", "heading": "Ausgangslage", "order": 1, "updatedAt": "2026-09-22T08:14:00Z" },
                { "id": "s_3c4d", "heading": "Bewertung", "order": 2, "updatedAt": "2026-09-22T08:14:00Z" }
              ],
              "createdAt": "2026-09-19T10:00:00Z",
              "updatedAt": "2026-09-22T08:14:00Z"
            }
            """);

        var loaded = Assert.IsType<DocumentResult.Success>(
            await _service.LoadDocumentAsync(new DocumentIdentifier(DocumentId), Token));

        Assert.Equal("Gutachten Musterstraße", loaded.Document.Title);
        Assert.Equal(DocumentState.InReview, loaded.Document.State);
        Assert.Equal(4, loaded.Document.Version);
        Assert.Equal(["s_1a2b", "s_3c4d"], loaded.Document.Sections.Select(section => section.Id.Value));
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero), loaded.Document.CreatedAt);
    }

    // Approved documents require explicit reopening; legacy files obey the same guard.
    [Fact]
    public async Task An_approved_document_refuses_changes_until_reopened()
    {
        await WriteFileAsync("""
            {
              "id": "d7kq2fr",
              "ownerId": "owner",
              "title": "Gutachten",
              "state": "Approved",
              "version": 4,
              "sections": [],
              "createdAt": "2026-09-19T10:00:00Z",
              "updatedAt": "2026-09-22T08:14:00Z"
            }
            """);
        var loaded = Assert.IsType<DocumentResult.Success>(
            await _service.LoadDocumentAsync(new DocumentIdentifier(DocumentId), Token));

        Assert.IsType<DocumentResult.Conflict>(await _service.RenameDocumentAsync(
            loaded.Document.Id,
            "Gutachten Musterstraße",
            loaded.ETag,
            Token));

        Assert.Equal(DocumentState.Approved, loaded.Document.State);
        Assert.Equal(4, loaded.Document.Version);
        Assert.Contains("\"state\": \"Approved\"", await ReadFileAsync());
    }

    // The list is the authority over the order field: somebody who rescues a
    // file by hand moves an entry, and the numbers follow. The other way round -
    // taking the numbers and sorting by them - would make two records of the
    // same thing able to disagree.
    [Fact]
    public async Task The_position_in_the_list_decides_the_order_and_the_numbers_follow()
    {
        await WriteFileAsync("""
            {
              "id": "d7kq2fr",
              "ownerId": "owner",
              "title": "Gutachten",
              "state": "Draft",
              "version": 0,
              "sections": [
                { "id": "s_3c4d", "heading": "Bewertung", "order": 2, "updatedAt": "2026-09-22T08:14:00Z" },
                { "id": "s_1a2b", "heading": "Ausgangslage", "order": 1, "updatedAt": "2026-09-22T08:14:00Z" }
              ],
              "createdAt": "2026-09-19T10:00:00Z",
              "updatedAt": "2026-09-22T08:14:00Z"
            }
            """);

        var loaded = Assert.IsType<DocumentResult.Success>(
            await _service.LoadDocumentAsync(new DocumentIdentifier(DocumentId), Token));

        Assert.Equal(["s_3c4d", "s_1a2b"], loaded.Document.Sections.Select(section => section.Id.Value));
        Assert.Equal([1, 2], loaded.Document.Sections.Select(section => section.Order));
    }

    // A file this application did not write is a defect and not a case the user
    // interface has a sentence for, so it is reported as an exception and reaches
    // the error page - unlike a missing document or a failed condition, which are
    // result values.
    [Theory]
    [InlineData("{ \"id\": \"d7kq2fr\" }")]
    [InlineData("nicht einmal JSON")]
    [InlineData("null")]
    public async Task A_file_that_is_not_of_this_model_is_refused(string content)
    {
        await WriteFileAsync(content);

        await Assert.ThrowsAsync<JsonException>(
            () => _service.LoadDocumentAsync(new DocumentIdentifier(DocumentId), Token));
    }

    [Fact]
    public async Task A_state_the_model_does_not_know_is_refused()
    {
        await WriteFileAsync("""
            {
              "id": "d7kq2fr",
              "ownerId": "owner",
              "title": "Gutachten",
              "state": "Freigegeben",
              "version": 0,
              "sections": [],
              "createdAt": "2026-09-19T10:00:00Z",
              "updatedAt": "2026-09-22T08:14:00Z"
            }
            """);

        await Assert.ThrowsAsync<JsonException>(
            () => _service.LoadDocumentAsync(new DocumentIdentifier(DocumentId), Token));
    }

    // An identifier with an upper case letter could be written into a file by
    // hand but never be turned into a path, so it is caught when the file is
    // read instead of when a section text is next saved.
    [Fact]
    public async Task A_section_identifier_outside_the_alphabet_is_refused()
    {
        await WriteFileAsync("""
            {
              "id": "d7kq2fr",
              "ownerId": "owner",
              "title": "Gutachten",
              "state": "Draft",
              "version": 0,
              "sections": [
                { "id": "S_1a2b", "heading": "Ausgangslage", "order": 1, "updatedAt": "2026-09-22T08:14:00Z" }
              ],
              "createdAt": "2026-09-19T10:00:00Z",
              "updatedAt": "2026-09-22T08:14:00Z"
            }
            """);

        await Assert.ThrowsAsync<JsonException>(
            () => _service.LoadDocumentAsync(new DocumentIdentifier(DocumentId), Token));
    }

    /// <summary>Removes the directory of this test.</summary>
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

    /// <summary>Puts a <c>document.json</c> into the store as it stands.</summary>
    private async Task WriteFileAsync(string content) =>
        Assert.IsType<ObjectWriteResult.Written>(await _objects.WriteAsync(
            $"documents/{DocumentId}/document.json",
            content,
            WriteCondition.Unconditional,
            Token));

    /// <summary>Reads the <c>document.json</c> back as it stands.</summary>
    private async Task<string> ReadFileAsync() =>
        Assert.IsType<ObjectReadResult.Found>(
            await _objects.ReadAsync($"documents/{DocumentId}/document.json", Token)).Content;
}
