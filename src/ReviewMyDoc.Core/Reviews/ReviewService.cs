// Coordinates collecting, issuing and returning reviews. Public access checks
// the token hash on every operation; the public view receives only quotations.
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Reviews;

/// <summary>The complete collected-passage review lifecycle.</summary>
public sealed class ReviewService(ReviewStore reviews, IDocumentStore documents, DocumentService documentService, TimeProvider clock,
    DocumentEditingService editing)
{
    /// <summary>The same conflict message is used for every stale write.</summary>
    public const string Conflict = "Inzwischen geändert. Bitte neu laden; deine Eingabe wurde nicht überschrieben.";

    /// <summary>Hashes text or a token without persisting the original secret.</summary>
    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Loads an assignment for its owner.</summary>
    public Task<StoredReview?> LoadAsync(DocumentIdentifier documentId, string reviewId, CancellationToken ct) =>
        reviews.ReadAsync(documentId, reviewId, ct);

    /// <summary>Lists assignments for an owner's document.</summary>
    public Task<IReadOnlyList<StoredReview>> ListAsync(DocumentIdentifier documentId, CancellationToken ct) => reviews.ListAsync(documentId, ct);

    /// <summary>Creates a named empty review set.</summary>
    public Task<ReviewResult> CreateSetAsync(DocumentIdentifier documentId, string? name, CancellationToken ct) =>
        new ReviewCollectionService(reviews, documents, clock).CreateAsync(documentId, name, ct);

    /// <summary>Ends or resumes passage collection while preserving later assignment.</summary>
    public Task<ReviewResult> SetCollectionClosedAsync(DocumentIdentifier documentId, string reviewId, string etag, bool closed, CancellationToken ct) =>
        new ReviewCollectionService(reviews, documents, clock).SetClosedAsync(documentId, reviewId, etag, closed, ct);

    /// <summary>Persists a selection against the source version shown in the editor.</summary>
    public async Task<ReviewResult> CollectAsync(DocumentIdentifier documentId, SectionIdentifier sectionId,
        string markdown, string textETag, string? draftId, string? draftETag, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(markdown) || markdown.Length > 50_000) { return new(Error: "Bitte eine Passage mit höchstens 50.000 Zeichen markieren."); }
        var document = await documents.ReadAsync(documentId, ct);
        var section = document?.Document.FindSection(sectionId);
        var text = section is null ? null : await documents.ReadSectionTextAsync(documentId, sectionId, ct);
        if (document is null || document.Document.State == DocumentState.Approved || section is null || text is null || text.ETag.Value != textETag) { return new(Error: Conflict); }

        var existing = draftId is null ? null : await reviews.ReadAsync(documentId, draftId, ct);
        if (draftId is not null && (existing is null || existing.ETag.Value != draftETag || existing.Review.State != "Draft" || existing.Review.CollectionClosedAt is not null))
        {
            return new(Error: Conflict);
        }

        var draft = existing?.Review ?? new ReviewAssignment(DocumentIdentifier.Draw().Value, documentId.Value,
            document.Document.Title, "Draft", [], clock.GetUtcNow());
        if (draft.Passages.Count >= 100) { return new(Error: "Ein Auftrag kann höchstens 100 Passagen enthalten."); }
        var passage = new ReviewPassage(DocumentIdentifier.Draw().Value, sectionId.Value, section.Heading, markdown.Trim(), Hash(text.Content));
        var changed = draft with { Passages = [.. draft.Passages, passage] };
        return await WriteAsync(changed, existing, ct);
    }

    /// <summary>Removes a collected passage without changing the document text.</summary>
    public async Task<ReviewResult> RemoveAsync(DocumentIdentifier documentId, string reviewId, string passageId, string etag, CancellationToken ct)
    {
        var stored = await reviews.ReadAsync(documentId, reviewId, ct);
        if (stored is null || stored.ETag.Value != etag || stored.Review.State != "Draft" || stored.Review.CollectionClosedAt is not null) { return new(Error: Conflict); }
        return await WriteAsync(stored.Review with { Passages = [.. stored.Review.Passages.Where(p => p.Id != passageId)] }, stored, ct);
    }

    /// <summary>Freezes a version and issues a review link for the collected excerpts.</summary>
    public async Task<ReviewResult> IssueAsync(DocumentIdentifier documentId, string reviewId, string etag,
        string name, string? email, DateTimeOffset dueAt, CancellationToken ct, string visibility = "AssignedSectionsOnly")
    {
        if (visibility is not ("AssignedSectionsOnly" or "WholeDocument") || string.IsNullOrWhiteSpace(name) || name.Length > 150 || (email?.Length ?? 0) > 254 ||
            (!string.IsNullOrWhiteSpace(email) && !MailAddress.TryCreate(email, out _)) ||
            dueAt <= clock.GetUtcNow() || dueAt > clock.GetUtcNow().AddYears(1))
        {
            return new(Error: "Bitte einen Namen, eine gültige optionale Mailadresse und eine zukünftige Frist innerhalb eines Jahres angeben.");
        }

        var stored = await reviews.ReadAsync(documentId, reviewId, ct);
        if (stored is null || stored.ETag.Value != etag || stored.Review.State != "Draft" || stored.Review.Passages.Count == 0) { return new(Error: Conflict); }
        var doc = await documents.ReadAsync(documentId, ct);
        if (doc is null || doc.Document.State == DocumentState.Approved) { return new(Error: Conflict); }
        foreach (var passage in stored.Review.Passages)
        {
            var sectionId = new SectionIdentifier(passage.SectionId);
            var text = doc.Document.FindSection(sectionId) is null ? null : await documents.ReadSectionTextAsync(documentId, sectionId, ct);
            if (text is null || Hash(text.Content) != passage.SourceHash)
            {
                return new(Error: "Der Text einer gesammelten Passage wurde geändert. Entferne diese Passage und markiere sie erneut.");
            }
        }

        var frozen = await documentService.FreezeVersionAsync(documentId, doc.ETag, ct);
        if (frozen is not DocumentResult.Success success) { return new(Error: Conflict); }
        var version = await documents.ReadVersionAsync(documentId, success.Document.Version, ct);
        if (version is null || stored.Review.Passages.Any(p =>
            version.Sections.All(s => s.Id.Value != p.SectionId || Hash(s.Text) != p.SourceHash))) { return new(Error: Conflict); }

        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var changed = stored.Review with
        {
            State = "Sent",
            ReviewerName = name.Trim(),
            ReviewerEmail = email?.Trim() ?? "",
            DocumentVersion = version.Version,
            Title = version.Title,
            DueAt = dueAt,
            TokenHash = Hash(token),
            TokenExpiresAt = dueAt.AddDays(14),
            Visibility = visibility
        };
        var frozenDocument = await documents.ReadAsync(documentId, ct);
        if (frozenDocument is null || frozenDocument.Document.State == DocumentState.Approved ||
            await documents.WriteAsync(frozenDocument.Document.WithState(DocumentState.InReview, clock.GetUtcNow()),
                WriteCondition.MustMatch(frozenDocument.ETag), ct) is not ObjectWriteResult.Written) { return new(Error: Conflict); }
        var result = await WriteAsync(changed, stored, ct);
        return result.Error is null ? result with { Token = token } : result;
    }

    /// <summary>Authenticates a capability hash for exactly this assignment, including revocation and expiry.</summary>
    public async Task<StoredReview?> AccessAsync(DocumentIdentifier documentId, string reviewId, string tokenHash, CancellationToken ct)
    {
        var stored = await reviews.ReadAsync(documentId, reviewId, ct);
        if (stored is null || stored.Review.State is not ("Sent" or "Returned") || stored.Review.TokenExpiresAt is null || stored.Review.TokenExpiresAt <= clock.GetUtcNow() ||
            stored.Review.TokenHash is not { } expected || tokenHash.Length != expected.Length ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(tokenHash), Encoding.ASCII.GetBytes(expected))) { return null; }
        // Recovery data may contain a complete private source section. It must
        // never cross the capability boundary, even temporarily during application.
        return stored with
        {
            Review = stored.Review with
            {
                Passages = [.. stored.Review.Passages.Select(p =>
            p with { ApplicationText = null, ApplicationETag = null, ApplicationHash = null })]
            }
        };
    }

    /// <summary>Loads additional frozen context only after capability and visibility checks.</summary>
    public async Task<ReviewReading?> ReadForReviewerAsync(DocumentIdentifier documentId, string reviewId, string tokenHash, CancellationToken ct)
    {
        var stored = await AccessAsync(documentId, reviewId, tokenHash, ct);
        if (stored is null) { return null; }
        var version = stored.Review.Visibility == "WholeDocument"
            ? await documents.ReadVersionAsync(documentId, stored.Review.DocumentVersion, ct) : null;
        var changed = new List<string>();
        var current = await documents.ReadAsync(documentId, ct);
        foreach (var passage in stored.Review.Passages)
        {
            var sectionId = new SectionIdentifier(passage.SectionId);
            var text = current?.Document.FindSection(sectionId) is null ? null : await documents.ReadSectionTextAsync(documentId, sectionId, ct);
            if (text is null || Hash(text.Content) != passage.SourceHash) { changed.Add(passage.Id); }
        }
        return new(stored, version, changed);
    }

    /// <summary>Returns a review once, rejecting feedback on any unassigned passage.</summary>
    public async Task<ReviewResult> ReturnAsync(DocumentIdentifier documentId, string reviewId, string tokenHash, string etag,
        IReadOnlyDictionary<string, string> feedback, CancellationToken ct)
        => await ReturnFeedbackAsync(documentId, reviewId, tokenHash, etag,
            feedback.ToDictionary(p => p.Key, p => new ReviewFeedback(p.Value)), ct);

    /// <summary>Validates typed feedback against assigned passages, never whole-document context.</summary>
    public async Task<ReviewResult> ReturnFeedbackAsync(DocumentIdentifier documentId, string reviewId, string tokenHash, string etag,
        IReadOnlyDictionary<string, ReviewFeedback> feedback, CancellationToken ct)
    {
        var stored = await AccessAsync(documentId, reviewId, tokenHash, ct);
        if (stored is null || stored.ETag.Value != etag || stored.Review.State != "Sent") { return new(Error: Conflict); }
        if (feedback.Any(f => f.Value.Text.Length > 10_000 || (f.Value.ProposedMarkdown?.Length ?? 0) > 50_000 ||
            f.Value.Kind is not ("Comment" or "Suggestion" or "Question") ||
            (f.Value.Kind == "Question" && string.IsNullOrWhiteSpace(f.Value.Text)) ||
            (f.Value.Kind == "Suggestion" && string.IsNullOrWhiteSpace(f.Value.ProposedMarkdown)) ||
            stored.Review.Passages.All(p => p.Id != f.Key)))
        {
            return new(Error: "Die Rückmeldung gehört nicht zu diesem Auftrag oder ist zu lang.");
        }
        var passages = stored.Review.Passages.Select(p =>
        {
            var input = feedback.GetValueOrDefault(p.Id) ?? new ReviewFeedback("");
            return p with
            {
                Feedback = input.Text.Trim(),
                FeedbackKind = input.Kind,
                ProposedMarkdown = input.Kind == "Suggestion" ? input.ProposedMarkdown : null,
                FeedbackAt = clock.GetUtcNow(),
                Resolved = false
            };
        }).ToArray();
        return await WriteAsync(stored.Review with { Passages = passages, State = "Returned", ReturnedAt = clock.GetUtcNow() }, stored, ct);
    }

    /// <summary>Whether feedback still needs an explicit owner decision.</summary>
    public static bool IsOpen(ReviewPassage passage) => !passage.Resolved &&
        (!string.IsNullOrWhiteSpace(passage.Feedback) || passage.FeedbackKind is "Suggestion" or "Question");

    /// <summary>Resolves a comment, closes an assignment or revokes access.</summary>
    public Task<ReviewResult> DecideAsync(DocumentIdentifier documentId, string reviewId, string etag, string action, string? passageId, CancellationToken ct) =>
        new ReviewDecisionService(reviews, documents, editing, clock).DecideAsync(documentId, reviewId, etag, action, passageId, ct);
    /// <summary>Answers a question or rejects a suggestion.</summary>
    public Task<ReviewResult> RespondAsync(DocumentIdentifier documentId, string reviewId, string etag, string passageId, string action, string? answer, CancellationToken ct) =>
        new ReviewDecisionService(reviews, documents, editing, clock).RespondAsync(documentId, reviewId, etag, passageId, action, answer, ct);
    /// <summary>Applies a suggestion using the text version reviewed by the owner.</summary>
    public Task<ReviewResult> ApplySuggestionAsync(DocumentIdentifier documentId, string reviewId, string etag, string passageId, string textETag, CancellationToken ct) =>
        new ReviewDecisionService(reviews, documents, editing, clock).ApplySuggestionAsync(documentId, reviewId, etag, passageId, textETag, ct);
    /// <summary>Releases or explicitly reopens the document.</summary>
    public Task<string?> SetDocumentApprovalAsync(DocumentIdentifier documentId, string etag, bool approve, CancellationToken ct) =>
        new DocumentApprovalService(reviews, documents, clock).SetDocumentApprovalAsync(documentId, etag, approve, ct);
    private async Task<ReviewResult> WriteAsync(ReviewAssignment review, StoredReview? before, CancellationToken ct)
    {
        var written = await reviews.WriteAsync(review, before is null ? WriteCondition.MustNotExist : WriteCondition.MustMatch(before.ETag), ct);
        return written is ObjectWriteResult.Written success ? new(new StoredReview(review, success.ETag)) : new(Error: Conflict);
    }

}
