// Provides a fresh directory store and shared assertions for each document operation test.

using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Documents;

/// <summary>Isolates every test in its own real directory store.</summary>
public abstract class DocumentServiceTestBase : IDisposable
{
    /// <summary>The moment a document is created in these tests.</summary>
    protected static readonly DateTimeOffset Created = new(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The later moment at which it is changed.</summary>
    protected static readonly DateTimeOffset Changed = new(2026, 9, 22, 8, 14, 0, TimeSpan.Zero);

    protected readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "reviewmydoc-tests",
        Guid.NewGuid().ToString("n"));

    protected readonly FixedTimeProvider _clock = new(Created);
    protected readonly IObjectStore _objects;
    protected readonly DocumentService _service;

    /// <summary>Builds a service over an empty directory of this test.</summary>
    protected DocumentServiceTestBase()
    {
        _objects = new DirectoryObjectStore(_root);
        _service = new DocumentService(new DocumentStore(_objects), _clock);
    }

    protected static CancellationToken Token => CancellationToken.None;


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
    protected async Task<DocumentResult.Success> CreateDocumentAsync(string title) =>
        await SucceedsAsync(_service.CreateDocumentAsync("owner", title, Token));

    /// <summary>Creates a document with three sections, all stamped at <see cref="Created"/>.</summary>
    protected async Task<DocumentResult.Success> WithThreeSectionsAsync()
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
    protected async Task<(DocumentResult.Success Document, ETag Outdated)> OvertakenAsync()
    {
        var document = await WithThreeSectionsAsync();
        var outdated = document.ETag;
        var overtaken = await SucceedsAsync(
            _service.RenameDocumentAsync(document.Document.Id, "Zwischendurch umbenannt", outdated, Token));

        Assert.NotEqual(outdated, overtaken.ETag);

        return (document, outdated);
    }

    /// <summary>Insists that an operation took effect and hands back its result.</summary>
    protected static async Task<DocumentResult.Success> SucceedsAsync(Task<DocumentResult> operation) =>
        Assert.IsType<DocumentResult.Success>(await operation);

    /// <summary>The identifiers of the sections, in the order the outline holds them.</summary>
    protected static string[] Identifiers(Document document) =>
        [.. document.Sections.Select(section => section.Id.Value)];

    /// <summary>Reads one entry of the store, insisting that it is there.</summary>
    protected async Task<string> ReadEntryAsync(string path) =>
        Assert.IsType<ObjectReadResult.Found>(await _objects.ReadAsync(path, Token)).Content;

    /// <summary>Reads the Markdown text of the section at a position of the outline.</summary>
    protected async Task<string> ReadSectionTextAsync(Document document, int position) =>
        await ReadEntryAsync($"documents/{document.Id}/sections/{document.Sections[position].Id}.md");

    /// <summary>
    /// Puts a section's text directly into the store, bypassing the service - the
    /// section editor of a later story is what would normally do this.
    /// </summary>
    protected async Task WriteSectionTextAsync(DocumentIdentifier documentId, SectionIdentifier sectionId, string text) =>
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
    protected async Task AssertNoEntryWithoutFileAsync(Document document)
    {
        var texts = await _objects.ListAsync($"documents/{document.Id}/sections/", Token);

        foreach (var section in document.Sections)
        {
            Assert.Contains($"documents/{document.Id}/sections/{section.Id}.md", texts);
        }
    }

    /// <summary>Insists that a refused operation changed nothing on disk.</summary>
    protected async Task AssertUnchangedAsync(DocumentResult.Success document)
    {
        var loaded = await SucceedsAsync(_service.LoadDocumentAsync(document.Document.Id, Token));

        Assert.Equal(document.ETag, loaded.ETag);
        Assert.Equal(Identifiers(document.Document), Identifiers(loaded.Document));
    }
}
