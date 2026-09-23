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
        Collection = (await reviews.ListAsync(id!, ct)).FirstOrDefault(r => r.Review.State == "Draft");
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
    public async Task<IActionResult> OnPostCollectSectionAsync(string documentId, string sectionId, string etag, CancellationToken ct)
    {
        if (!TryId(documentId, out var id) || !TryId(sectionId, out _)) { return NotFound(); }
        var text = await store.ReadSectionTextAsync(id!, new SectionIdentifier(sectionId), ct);
        if (text is null) { return NotFound(); }
        var page = await OnGetAsync(documentId, ct);
        if (page is not PageResult) { return page; }
        var result = await reviews.CollectAsync(id!, new SectionIdentifier(sectionId), text.Content, etag,
            Collection?.Review.Id, Collection?.ETag.Value, ct);
        if (result.Error is not null) { Message = result.Error; Response.StatusCode = 409; return Page(); }
        return Redirect($"/dokumente/{documentId}/reviews/{result.Stored!.Review.Id}");
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
            : new JsonResult(new
            {
                id = result.Stored!.Review.Id,
                etag = result.Stored.ETag.Value,
                passages = result.Stored.Review.Passages.Select(p => new { p.Id, p.Heading, preview = Preview(p.Markdown) })
            });
    }

    private JsonResult ConflictResult() => new(new { error = ReviewService.Conflict }) { StatusCode = 409 };

    private static bool TryId(string? value, out DocumentIdentifier? id)
    {
        try { id = new DocumentIdentifier(value ?? ""); return true; }
        catch (ArgumentException) { id = null; return false; }
    }
}
