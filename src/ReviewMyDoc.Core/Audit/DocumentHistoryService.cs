// Resolves audit identifiers at read time, keeping private names out of the log.
// Deleted entities retain their identifiers instead of disappearing from history.
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Reviews;

namespace ReviewMyDoc.Core.Audit;

/// <summary>A readable event for the owner's document history.</summary>
public sealed record DocumentHistoryEntry(DateTimeOffset OccurredAt, string Description);

/// <summary>A document and its newest-first history.</summary>
public sealed record DocumentHistory(Document Document, IReadOnlyList<DocumentHistoryEntry> Entries);

/// <summary>Builds a readable history within the owner boundary.</summary>
public sealed class DocumentHistoryService(IDocumentStore documents, ReviewStore reviews, AuditLog audit)
{
    /// <summary>Loads one document's history, or null when the document does not exist.</summary>
    public async Task<DocumentHistory?> LoadAsync(DocumentIdentifier documentId, CancellationToken ct)
    {
        var document = await documents.ReadAsync(documentId, ct);
        if (document is null) { return null; }
        var assignments = (await reviews.ListAsync(documentId, ct)).ToDictionary(r => r.Review.Id, r => r.Review);
        string ReviewName(string? id) => id is not null && assignments.TryGetValue(id, out var review)
            ? string.IsNullOrWhiteSpace(review.ReviewerName) ? review.SetName : review.ReviewerName
            : id ?? "unbekannt";
        string Describe(AuditEvent entry)
        {
            var section = document.Document.Sections.FirstOrDefault(s => s.Id.Value == entry.SectionId)?.Heading ?? entry.SectionId;
            var review = ReviewName(entry.ReviewId);
            var subject = entry.Actor == AuditActor.Owner ? "Du hast" : $"{ReviewName(entry.ActorId)} hat";
            var action = entry.Kind switch
            {
                AuditEventKind.DocumentCreated => "das Dokument angelegt",
                AuditEventKind.DocumentRenamed => "das Dokument umbenannt",
                AuditEventKind.SectionAdded => $"den Abschnitt „{section}“ angelegt",
                AuditEventKind.SectionRenamed => $"den Abschnitt „{section}“ umbenannt",
                AuditEventKind.SectionsReordered => "die Abschnitte umsortiert",
                AuditEventKind.SectionDeleted => $"den Abschnitt „{section}“ gelöscht",
                AuditEventKind.SectionSplit => $"den Abschnitt „{section}“ geteilt",
                AuditEventKind.TextSaved => $"den Text im Abschnitt „{section}“ gespeichert",
                AuditEventKind.VersionFrozen => $"Fassung {entry.Version} eingefroren",
                AuditEventKind.ReviewSetCreated => $"das Review-Set „{review}“ angelegt",
                AuditEventKind.CollectionClosed => $"die Sammlung „{review}“ abgeschlossen",
                AuditEventKind.CollectionReopened => $"die Sammlung „{review}“ wieder geöffnet",
                AuditEventKind.PassageCollected => $"eine Passage aus „{section}“ im Review-Set „{review}“ gesammelt",
                AuditEventKind.PassageRemoved => $"die Passage „{entry.PassageId}“ aus „{review}“ entfernt",
                AuditEventKind.ReviewIssued => $"den Reviewauftrag an „{review}“ erteilt",
                AuditEventKind.ReviewReturned => $"den Reviewauftrag „{review}“ zurückgemeldet",
                AuditEventKind.FeedbackResolved => $"einen Kommentar zu „{section}“ im Reviewauftrag „{review}“ erledigt",
                AuditEventKind.QuestionAnswered => $"eine Rückfrage zu „{section}“ im Reviewauftrag „{review}“ beantwortet",
                AuditEventKind.SuggestionRejected => $"einen Änderungsvorschlag zu „{section}“ im Reviewauftrag „{review}“ abgelehnt",
                AuditEventKind.SuggestionApplied => $"einen Änderungsvorschlag zu „{section}“ im Reviewauftrag „{review}“ übernommen",
                AuditEventKind.ReviewAccepted => $"den Reviewauftrag „{review}“ abgenommen",
                AuditEventKind.ReviewRevoked => $"den Reviewauftrag „{review}“ widerrufen",
                AuditEventKind.DocumentApproved => "das Dokument freigegeben",
                AuditEventKind.DocumentReopened => "das Dokument wieder geöffnet",
                _ => "eine Änderung vorgenommen"
            };
            return $"{subject} {action}.";
        }

        var entries = await audit.ReadAsync(documentId, ct);
        return new(document.Document, entries.Select(e => new DocumentHistoryEntry(e.OccurredAt, Describe(e))).ToArray());
    }
}
