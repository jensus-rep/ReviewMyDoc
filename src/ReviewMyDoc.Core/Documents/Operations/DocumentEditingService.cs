// Reads and conditionally saves the text behind the writing surface. Section
// membership is checked here so pages cannot write arbitrary text blobs.
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Documents;

/// <summary>Text operations for the document editor.</summary>
public sealed class DocumentEditingService(IDocumentStore store, TimeProvider clock)
{
    /// <summary>Saves a section only against the text version the writer saw.</summary>
    public async Task<ObjectWriteResult> SaveAsync(DocumentIdentifier documentId, SectionIdentifier sectionId,
        string text, ETag expectedText, CancellationToken cancellationToken)
    {
        var document = await store.ReadAsync(documentId, cancellationToken);
        if (document?.Document.FindSection(sectionId) is null || document.Document.State == DocumentState.Approved || text.Length > 500_000)
        {
            return new ObjectWriteResult.Conflict();
        }

        var result = await store.WriteSectionTextAsync(documentId, sectionId, text,
            WriteCondition.MustMatch(expectedText), cancellationToken);
        if (result is ObjectWriteResult.Written)
        {
            // Text is authoritative. A simultaneous outline edit may win this
            // metadata update without turning an already saved text into a failure.
            var current = await store.ReadAsync(documentId, cancellationToken);
            if (current?.Document.FindSection(sectionId) is not null)
            {
                var now = clock.GetUtcNow();
                var value = current.Document;
                var touched = new Document(value.Id, value.OwnerId, value.Title, value.State, value.Version,
                    value.Sections.Select(s => s.Id == sectionId ? s with { UpdatedAt = now } : s).ToArray(), value.CreatedAt, now);
                await store.WriteAsync(touched, WriteCondition.MustMatch(current.ETag), cancellationToken);
            }
        }

        return result;
    }
}
