// Defines the allowlisted facts that can enter the document history. Free text
// and credentials deliberately have no place in an audit event.
namespace ReviewMyDoc.Core.Audit;

/// <summary>A completed domain change, without content or credentials.</summary>
public sealed record AuditEvent(DateTimeOffset OccurredAt, AuditEventKind Kind, AuditActor Actor,
    string ActorId, string DocumentId, string? SectionId = null, string? ReviewId = null,
    string? PassageId = null, int? Version = null);

/// <summary>The role that initiated a domain change.</summary>
public enum AuditActor
{
    /// <summary>The document owner.</summary>
    Owner,
    /// <summary>The recipient of one review assignment.</summary>
    Reviewer
}

/// <summary>The changes recorded in the document history.</summary>
public enum AuditEventKind
{
    /// <summary>A document was created.</summary>
    DocumentCreated,
    /// <summary>The document title changed.</summary>
    DocumentRenamed,
    /// <summary>A section was added.</summary>
    SectionAdded,
    /// <summary>A section heading changed.</summary>
    SectionRenamed,
    /// <summary>The outline order changed.</summary>
    SectionsReordered,
    /// <summary>A section left the outline.</summary>
    SectionDeleted,
    /// <summary>A section was split.</summary>
    SectionSplit,
    /// <summary>Section text was saved.</summary>
    TextSaved,
    /// <summary>An immutable version was committed.</summary>
    VersionFrozen,
    /// <summary>A named review set was created.</summary>
    ReviewSetCreated,
    /// <summary>A collection was closed.</summary>
    CollectionClosed,
    /// <summary>A collection was reopened.</summary>
    CollectionReopened,
    /// <summary>A passage was collected.</summary>
    PassageCollected,
    /// <summary>A passage was removed.</summary>
    PassageRemoved,
    /// <summary>A review was issued.</summary>
    ReviewIssued,
    /// <summary>The reviewer returned feedback.</summary>
    ReviewReturned,
    /// <summary>A comment was resolved.</summary>
    FeedbackResolved,
    /// <summary>A question was answered.</summary>
    QuestionAnswered,
    /// <summary>A suggestion was rejected.</summary>
    SuggestionRejected,
    /// <summary>A suggestion was applied.</summary>
    SuggestionApplied,
    /// <summary>A review was accepted.</summary>
    ReviewAccepted,
    /// <summary>A review was revoked.</summary>
    ReviewRevoked,
    /// <summary>The document was approved.</summary>
    DocumentApproved,
    /// <summary>The document was reopened for editing.</summary>
    DocumentReopened
}
