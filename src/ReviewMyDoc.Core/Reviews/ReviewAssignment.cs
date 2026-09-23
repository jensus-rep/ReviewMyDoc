// A collected set of passages becomes one immutable invitation. Feedback stays
// with its passage and shares the assignment's optimistic concurrency boundary.
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Reviews;

/// <summary>A quoted passage and the response to that exact quotation.</summary>
public sealed record ReviewPassage(string Id, string SectionId, string Heading, string Markdown,
    string SourceHash, string? Feedback = null, bool Resolved = false);

/// <summary>The persisted review assignment, including its draft collection.</summary>
public sealed record ReviewAssignment(string Id, string DocumentId, string Title, string State,
    IReadOnlyList<ReviewPassage> Passages, DateTimeOffset CreatedAt,
    string ReviewerName = "", string ReviewerEmail = "", int DocumentVersion = 0,
    DateTimeOffset? DueAt = null, string? TokenHash = null, DateTimeOffset? TokenExpiresAt = null,
    DateTimeOffset? ReturnedAt = null);

/// <summary>A review together with the version that guards its next change.</summary>
public sealed record StoredReview(ReviewAssignment Review, ETag ETag);

/// <summary>An expected refusal or a successful change; only issuance returns a raw token.</summary>
public sealed record ReviewResult(StoredReview? Stored = null, string? Error = null, string? Token = null);
