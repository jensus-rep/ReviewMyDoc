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
            if (loaded.Document.Sections.Count != 0 || loaded.ETag.Value != documentETag) { return ConflictResult(); }
            var added = await documents.AddSectionAsync(id!, "Dokumenttext", loaded.ETag, ct);
            if (added is not DocumentResult.Success success) { return ConflictResult(); }
            sectionId = success.Document.Sections[0].Id.Value;
            etag = (await store.ReadSectionTextAsync(id!, new SectionIdentifier(sectionId), ct))!.ETag.Value;
        }
        if (!TryId(sectionId, out _) || string.IsNullOrEmpty(etag)) { return BadRequest(); }
        var result = await editing.SaveAsync(id!, new SectionIdentifier(sectionId), markdown ?? "", new ETag(etag), ct);
        if (result is not ObjectWriteResult.Written written) { return ConflictResult(); }
        return Request.Headers["X-Requested-With"] == "fetch"
            ? new JsonResult(new { sectionId, etag = written.ETag.Value })
            : Redirect($"/dokumente/{documentId}");
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
