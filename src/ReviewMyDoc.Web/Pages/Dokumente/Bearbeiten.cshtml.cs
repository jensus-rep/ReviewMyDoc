// The writing page loads safe, rendered prose and saves Markdown conditionally.
// Selections become review quotations without changing the document structure.
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Markdown;
using ReviewMyDoc.Core.Reviews;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Web.Pages.Dokumente;

/// <summary>A section as displayed on the continuous writing surface.</summary>
public sealed record EditorSection(string Id, string Heading, string Markdown, string Html, string ETag);

/// <summary>Owner-facing document editor.</summary>
public sealed class BearbeitenModel(IDocumentStore store, DocumentService documents, DocumentEditingService editing,
    IMarkdownRenderer renderer, ReviewService reviews) : PageModel
{
    /// <summary>The loaded document.</summary>
    public StoredDocument Document { get; private set; } = null!;
    /// <summary>Sections, rendered through the application's safe renderer.</summary>
    public IReadOnlyList<EditorSection> Sections { get; private set; } = [];
    /// <summary>The collection resumed when this document is reopened.</summary>
    public StoredReview? Collection { get; private set; }
    /// <summary>Separate draft collections that can later be assigned to different people.</summary>
    public IReadOnlyList<StoredReview> Collections { get; private set; } = [];
    /// <summary>Assigned and completed sets, available on demand outside the selection menu.</summary>
    public IReadOnlyList<StoredReview> ReviewHistory { get; private set; } = [];
    /// <summary>A form validation or conflict message.</summary>
    public string? Message { get; private set; }

    /// <summary>A plain excerpt for the collection, without Markdown syntax.</summary>
    public string Preview(string markdown)
    {
        var plain = WebUtility.HtmlDecode(Regex.Replace(renderer.Render(markdown), "<[^>]*>", " "));
        plain = Regex.Replace(plain, @"\s+", " ").Trim();
        return plain.Length > 140 ? plain[..140] + "…" : plain;
    }

    /// <summary>Opens the document without creating or changing any content.</summary>
    public async Task<IActionResult> OnGetAsync(string documentId, CancellationToken ct)
    {
        if (!TryId(documentId, out var id)) { return NotFound(); }
        var loaded = await store.ReadAsync(id!, ct);
        if (loaded is null) { return NotFound(); }
        Document = loaded;
        var sections = new List<EditorSection>();
        foreach (var entry in loaded.Document.Sections)
        {
            var text = await store.ReadSectionTextAsync(id!, entry.Id, ct);
            sections.Add(new(entry.Id.Value, entry.Heading, text?.Content ?? "", renderer.Render(text?.Content ?? ""), text?.ETag.Value ?? ""));
        }
        Sections = sections;
        var sets = (await reviews.ListAsync(id!, ct)).OrderBy(r => r.Review.CreatedAt).ThenBy(r => r.Review.Id).ToArray();
        Collections = sets.Where(r => r.Review.State == "Draft").ToArray();
        Collection = Collections.FirstOrDefault(r => r.Review.CollectionClosedAt is null);
        ReviewHistory = sets.Where(r => r.Review.State != "Draft").ToArray();
        return Page();
    }

    /// <summary>Saves a text; the very first write creates its initial section.</summary>
    public async Task<IActionResult> OnPostSaveAsync(string documentId, string? sectionId, string? markdown,
        string? etag, string? documentETag, CancellationToken ct)
    {
        if (!TryId(documentId, out var id) || markdown?.Length > 500_000) { return BadRequest(); }
        if (string.IsNullOrEmpty(sectionId))
        {
            var loaded = await store.ReadAsync(id!, ct);
            if (loaded is null) { return NotFound(); }
            if (loaded.Document.Sections.Count != 0 || loaded.ETag.Value != documentETag) { return await SaveConflictAsync(documentId, sectionId, markdown, etag, ct); }
            var added = await documents.AddSectionAsync(id!, "Dokumenttext", loaded.ETag, ct);
            if (added is not DocumentResult.Success success) { return await SaveConflictAsync(documentId, sectionId, markdown, etag, ct); }
            sectionId = success.Document.Sections[0].Id.Value;
            etag = (await store.ReadSectionTextAsync(id!, new SectionIdentifier(sectionId), ct))!.ETag.Value;
        }
        if (!TryId(sectionId, out _) || string.IsNullOrEmpty(etag)) { return BadRequest(); }
        var result = await editing.SaveAsync(id!, new SectionIdentifier(sectionId), markdown ?? "", new ETag(etag), ct);
        if (result is not ObjectWriteResult.Written written) { return await SaveConflictAsync(documentId, sectionId, markdown, etag, ct); }
        return Request.Headers["X-Requested-With"] == "fetch"
            ? new JsonResult(new { sectionId, etag = written.ETag.Value })
            : Redirect($"/dokumente/{documentId}");
    }

    /// <summary>Changes approval only after checking the current document and its assignments.</summary>
    public async Task<IActionResult> OnPostApprovalAsync(string documentId, string etag, bool approve, CancellationToken ct)
    {
        if (!TryId(documentId, out var id)) { return NotFound(); }
        var error = await reviews.SetDocumentApprovalAsync(id!, etag, approve, ct);
        if (error is null) { return Redirect($"/dokumente/{documentId}"); }
        var page = await OnGetAsync(documentId, ct);
        Message = error;
        Response.StatusCode = 409;
        return page;
    }

    /// <summary>Collects a complete section using ordinary HTML forms without JavaScript.</summary>
    public async Task<IActionResult> OnPostCollectSectionAsync(string documentId, string sectionId, string etag, CancellationToken ct, string? draftId = null)
    {
        if (!TryId(documentId, out var id) || !TryId(sectionId, out _)) { return NotFound(); }
        var text = await store.ReadSectionTextAsync(id!, new SectionIdentifier(sectionId), ct);
        if (text is null) { return NotFound(); }
        var page = await OnGetAsync(documentId, ct);
        if (page is not PageResult) { return page; }
        var target = draftId is null ? Collection : Collections.FirstOrDefault(r => r.Review.Id == draftId && r.Review.CollectionClosedAt is null);
        if (draftId is not null && target is null) { Message = ReviewService.Conflict; Response.StatusCode = 409; return Page(); }
        var result = await reviews.CollectAsync(id!, new SectionIdentifier(sectionId), text.Content, etag,
            target?.Review.Id, target?.ETag.Value, ct);
        if (result.Error is not null) { Message = result.Error; Response.StatusCode = 409; return Page(); }
        return Redirect($"/dokumente/{documentId}#review-collection");
    }

    private async Task<IActionResult> SaveConflictAsync(string documentId, string? sectionId, string? markdown, string? etag, CancellationToken ct)
    {
        if (Request.Headers["X-Requested-With"] == "fetch") { return ConflictResult(); }
        var page = await OnGetAsync(documentId, ct);
        if (page is not PageResult) { return page; }
        var original = Sections.FirstOrDefault(s => s.Id == sectionId);
        var preserved = new EditorSection(sectionId ?? "", original?.Heading ?? "Nicht gespeicherter Text", markdown ?? "",
            renderer.Render(markdown ?? ""), etag ?? "");
        Sections = original is null ? [.. Sections, preserved] : [.. Sections.Select(s => s.Id == sectionId ? preserved : s)];
        Message = "Nicht gespeichert: Der Stand wurde geändert oder das Dokument ist freigegeben. Deine Eingabe bleibt unten erhalten. Öffne den aktuellen Stand in einem zweiten Tab und gleiche die Texte ab.";
        Response.StatusCode = 409;
        return Page();
    }

    /// <summary>Collects a passage from the saved version of its source section.</summary>
    public async Task<IActionResult> OnPostCollectAsync(string documentId, string sectionId, string markdown,
        string etag, string? draftId, string? draftETag, CancellationToken ct)
    {
        if (!TryId(documentId, out var id) || !TryId(sectionId, out _)) { return BadRequest(); }
        var result = await reviews.CollectAsync(id!, new SectionIdentifier(sectionId), markdown, etag, draftId, draftETag, ct);
        return result.Error is not null ? new JsonResult(new { error = result.Error }) { StatusCode = 409 }
            : new JsonResult(CollectionData(result.Stored!));
    }

    /// <summary>Only presentation data crosses into the collection controls.</summary>
    public object CollectionData(StoredReview stored) => new
    {
        id = stored.Review.Id,
        etag = stored.ETag.Value,
        name = stored.Review.SetName,
        closed = stored.Review.CollectionClosedAt is not null,
        passages = stored.Review.Passages.Select(p => new { id = p.Id, heading = p.Heading, preview = Preview(p.Markdown) })
    };

    /// <summary>Creates a named set without requiring a text selection.</summary>
    public async Task<IActionResult> OnPostCreateSetAsync(string documentId, string? setName, CancellationToken ct)
    {
        if (!TryId(documentId, out var id)) { return BadRequest(); }
        return await SetResultAsync(documentId, await reviews.CreateSetAsync(id!, setName, ct), ct);
    }

    /// <summary>Closes or reopens collecting while keeping the set assignable.</summary>
    public async Task<IActionResult> OnPostCloseSetAsync(string documentId, string reviewId, string etag, bool closed, CancellationToken ct)
    {
        if (!TryId(documentId, out var id) || !TryId(reviewId, out _)) { return BadRequest(); }
        return await SetResultAsync(documentId, await reviews.SetCollectionClosedAsync(id!, reviewId, etag, closed, ct), ct);
    }

    private async Task<IActionResult> SetResultAsync(string documentId, ReviewResult result, CancellationToken ct)
    {
        if (Request.Headers["X-Requested-With"] == "fetch")
        {
            return result.Error is null ? new JsonResult(CollectionData(result.Stored!))
                : new JsonResult(new { error = result.Error }) { StatusCode = 409 };
        }
        if (result.Error is null) { return Redirect($"/dokumente/{documentId}#review-collection"); }
        var page = await OnGetAsync(documentId, ct);
        Message = result.Error;
        Response.StatusCode = 409;
        return page;
    }

    private JsonResult ConflictResult() => new(new { error = ReviewService.Conflict }) { StatusCode = 409 };

    private static bool TryId(string? value, out DocumentIdentifier? id)
    {
        try { id = new DocumentIdentifier(value ?? ""); return true; }
        catch (ArgumentException) { id = null; return false; }
    }
}
