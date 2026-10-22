// Owns the append-only document history and its JSON mapping. An audit failure
// must never turn an already committed domain change into an apparent refusal.
using System.Text.Json;
using System.Text.Json.Serialization;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Audit;

/// <summary>Stores safe domain events and reads the history of one document.</summary>
public sealed class AuditLog(IObjectStore objects, TimeProvider clock, IAuditFailureReporter failures)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string PathOf(DocumentIdentifier id) => $"documents/{id.Value}/audit.log";

    /// <summary>Appends exactly one event after a successful domain write.</summary>
    public async Task RecordAsync(DocumentIdentifier documentId, AuditEventKind kind, CancellationToken ct,
        string? sectionId = null, string? reviewId = null, string? passageId = null, int? version = null,
        AuditActor actor = AuditActor.Owner)
    {
        // Only domain identifiers belong in these fields. Callers never pass
        // user content, credentials or exception messages to this method.
        if (sectionId is not null) { _ = new SectionIdentifier(sectionId); }
        if (reviewId is not null) { _ = new DocumentIdentifier(reviewId); }
        if (passageId is not null) { _ = new DocumentIdentifier(passageId); }
        var entry = new AuditEvent(clock.GetUtcNow(), kind, actor,
            actor == AuditActor.Reviewer ? reviewId! : "owner", documentId.Value,
            sectionId, reviewId, passageId, version);
        try
        {
            await objects.AppendLineAsync(PathOf(documentId), JsonSerializer.Serialize(entry, Json), ct);
        }
        catch (Exception exception) when (exception is ObjectStoreException or OperationCanceledException)
        {
            // The authoritative write is already committed. Retrying the domain
            // operation here would duplicate it; report only this audit gap.
            failures.Report(documentId, kind, exception.GetType().Name);
        }
    }

    /// <summary>Reads newest events first, preserving append order for equal timestamps.</summary>
    public async Task<IReadOnlyList<AuditEvent>> ReadAsync(DocumentIdentifier documentId, CancellationToken ct)
    {
        var stored = await objects.ReadAsync(PathOf(documentId), ct);
        if (stored is not ObjectReadResult.Found found) { return []; }
        return found.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<AuditEvent>(line, Json)
                ?? throw new JsonException("An audit entry cannot be null."))
            .Reverse().OrderByDescending(entry => entry.OccurredAt).ToArray();
    }
}
