// Maps assignments onto object storage. All review paths and JSON options are
// owned here; neither the pages nor the service assemble storage paths.
using System.Text.Json;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Reviews;

/// <summary>Persistence for review assignments.</summary>
public sealed class ReviewStore(IObjectStore objects)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string Prefix(DocumentIdentifier documentId) => $"documents/{documentId.Value}/reviews/";
    private static string PathOf(DocumentIdentifier documentId, string reviewId) =>
        Prefix(documentId) + new DocumentIdentifier(reviewId).Value + ".json";

    /// <summary>Reads one assignment.</summary>
    public async Task<StoredReview?> ReadAsync(DocumentIdentifier documentId, string reviewId, CancellationToken ct)
    {
        var result = await objects.ReadAsync(PathOf(documentId, reviewId), ct);
        return result is ObjectReadResult.Found found
            ? new StoredReview(JsonSerializer.Deserialize<ReviewAssignment>(found.Content, Json)!, found.ETag) : null;
    }

    /// <summary>Writes one assignment conditionally.</summary>
    public Task<ObjectWriteResult> WriteAsync(ReviewAssignment review, WriteCondition condition, CancellationToken ct) =>
        objects.WriteAsync(PathOf(new DocumentIdentifier(review.DocumentId), review.Id),
            JsonSerializer.Serialize(review, Json), condition, ct);

    /// <summary>Lists the assignments for one document.</summary>
    public async Task<IReadOnlyList<StoredReview>> ListAsync(DocumentIdentifier documentId, CancellationToken ct)
    {
        var result = new List<StoredReview>();
        foreach (var path in await objects.ListAsync(Prefix(documentId), ct))
        {
            if (!path.EndsWith(".json", StringComparison.Ordinal)) { continue; }
            var entry = await objects.ReadAsync(path, ct);
            if (entry is ObjectReadResult.Found found)
            {
                result.Add(new StoredReview(JsonSerializer.Deserialize<ReviewAssignment>(found.Content, Json)!, found.ETag));
            }
        }

        return result;
    }
}
