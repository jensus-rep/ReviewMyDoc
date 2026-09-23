// Page model of the outline of one document at /dokumente/{documentId}/gliederung: list
// the sections, and create, rename, reorder and delete them. It is a thin
// layer over DocumentService, which already carries every domain operation
// and the optimistic-locking rule of docs/Datenmodell.md - this page only
// binds forms to it and turns each DocumentResult into markup.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Web.Pages.Dokumente;

/// <summary>Model of the page at <c>/dokumente/{documentId}/gliederung</c>.</summary>
public sealed class GliederungModel : PageModel
{
    /// <summary>What the page says when a heading is left empty.</summary>
    public const string EmptyHeadingMessage = "Bitte eine Überschrift eingeben.";

    /// <summary>
    /// What the page says on <see cref="DocumentResult.Conflict"/> and on the
    /// two results that mean the same thing for the reader - the section a
    /// form named is gone, or the order it sent no longer fits - because in
    /// every one of the three cases the document moved on since the page was
    /// loaded and the honest answer is the same: reload and look again.
    /// </summary>
    public const string ConflictMessage = "Das Dokument wurde inzwischen geändert. Bitte die Seite neu laden.";

    private readonly DocumentService _documents;

    /// <summary>Takes the service every change on this page goes through.</summary>
    /// <param name="documents">Where the document and its outline are read from and written to.</param>
    public GliederungModel(DocumentService documents) => _documents = documents;

    /// <summary>The identifier of the document, as it stands in the address.</summary>
    public string DocumentId { get; private set; } = string.Empty;

    /// <summary>The title of the document.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>The German word for the state of the document.</summary>
    public string StateLabel { get; private set; } = string.Empty;

    /// <summary>The outline, already in the order it is shown.</summary>
    public IReadOnlyList<Section> Sections { get; private set; } = [];

    /// <summary>
    /// The version that was current when the page was rendered. Every form on
    /// the page carries it as a hidden field, so the next write is checked
    /// against exactly the state the visitor saw - assurance 1 of
    /// docs/Datenmodell.md, as this page has to keep it.
    /// </summary>
    public string ETag { get; private set; } = string.Empty;

    /// <summary>
    /// The identifier of the section a delete link asked to confirm, read from
    /// the query string of a GET. Set, the page shows the confirmation of that
    /// one row instead of its usual actions - the "eigene Bestätigung im
    /// Seitenfluss" the task asks for, in place of a browser <c>confirm()</c>
    /// the Content Security Policy of this application would refuse to run
    /// anyway.
    /// </summary>
    public string? PendingDeleteSectionId { get; private set; }

    /// <summary>The message shown in the frame of the page, above the outline, or nothing.</summary>
    public string? Message { get; private set; }

    /// <summary>The heading typed into the form that appends a new section.</summary>
    [BindProperty]
    public string? NewHeading { get; set; }

    /// <summary>The message beside the field of <see cref="NewHeading"/> after a refused attempt.</summary>
    public string? NewHeadingError { get; private set; }

    /// <summary>Which section a rename form posted for.</summary>
    [BindProperty]
    public string? RenameSectionId { get; set; }

    /// <summary>The heading typed into a rename form.</summary>
    [BindProperty]
    public string? RenameHeading { get; set; }

    /// <summary>The message beside the field of the section named by <see cref="RenameSectionId"/>.</summary>
    public string? RenameError { get; private set; }

    /// <summary>
    /// Which section a move-up, move-down or confirmed-delete form posted for.
    /// </summary>
    [BindProperty]
    public string? SectionId { get; set; }

    /// <summary>
    /// The version every mutating form carries, read back from its hidden
    /// field.
    /// </summary>
    [BindProperty]
    public string? ETagValue { get; set; }

    /// <summary>
    /// Every section identifier of the outline, in the order the page showed
    /// them in - the snapshot a move-up or move-down form carries alongside
    /// <see cref="ETagValue"/>, because the swap has to happen on the order
    /// the visitor actually saw and not on one read again at the moment of the
    /// write, which would defeat the very conflict this page has to catch.
    /// </summary>
    [BindProperty]
    public List<string>? SectionOrder { get; set; }

    /// <summary>Loads the outline.</summary>
    /// <param name="documentId">The identifier out of the address.</param>
    /// <param name="confirmDelete">The section a delete link asked to confirm, if any.</param>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    /// <returns>The page, or 404 for a document that does not exist or never did.</returns>
    public async Task<IActionResult> OnGetAsync(
        string documentId,
        string? confirmDelete,
        CancellationToken cancellationToken)
    {
        var id = TryParseDocumentId(documentId);
        if (id is null)
        {
            return NotFound();
        }

        var loaded = await _documents.LoadDocumentAsync(id, cancellationToken);
        if (loaded is not DocumentResult.Success success)
        {
            return NotFound();
        }

        Apply(success);
        PendingDeleteSectionId = confirmDelete;

        return Page();
    }

    /// <summary>Appends a section with the heading of <see cref="NewHeading"/>.</summary>
    /// <param name="documentId">The identifier out of the address.</param>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    public async Task<IActionResult> OnPostAddSectionAsync(string documentId, CancellationToken cancellationToken)
    {
        var id = TryParseDocumentId(documentId);
        if (id is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(NewHeading))
        {
            return await ShowValidationErrorAsync(id, cancellationToken, isNewHeading: true);
        }

        if (ETagValue is null)
        {
            return BadRequest();
        }

        var result = await _documents.AddSectionAsync(id, NewHeading, new ETag(ETagValue), cancellationToken);

        return await ApplyResultAsync(id, result, cancellationToken);
    }

    /// <summary>Gives the section named by <see cref="RenameSectionId"/> the heading of <see cref="RenameHeading"/>.</summary>
    /// <param name="documentId">The identifier out of the address.</param>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    public async Task<IActionResult> OnPostRenameSectionAsync(string documentId, CancellationToken cancellationToken)
    {
        var id = TryParseDocumentId(documentId);
        if (id is null)
        {
            return NotFound();
        }

        if (!TryParseSectionId(RenameSectionId, out var sectionId) || ETagValue is null)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(RenameHeading))
        {
            return await ShowValidationErrorAsync(id, cancellationToken, isNewHeading: false);
        }

        var result = await _documents.RenameSectionAsync(
            id,
            sectionId,
            RenameHeading,
            new ETag(ETagValue),
            cancellationToken);

        return await ApplyResultAsync(id, result, cancellationToken);
    }

    /// <summary>Moves the section named by <see cref="SectionId"/> one place towards the start.</summary>
    /// <param name="documentId">The identifier out of the address.</param>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    public Task<IActionResult> OnPostMoveUpAsync(string documentId, CancellationToken cancellationToken) =>
        MoveAsync(documentId, offset: -1, cancellationToken);

    /// <summary>Moves the section named by <see cref="SectionId"/> one place towards the end.</summary>
    /// <param name="documentId">The identifier out of the address.</param>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    public Task<IActionResult> OnPostMoveDownAsync(string documentId, CancellationToken cancellationToken) =>
        MoveAsync(documentId, offset: 1, cancellationToken);

    /// <summary>
    /// Removes the section named by <see cref="SectionId"/>, after the
    /// confirmation the query string <c>confirmDelete</c> of a GET already
    /// showed.
    /// </summary>
    /// <param name="documentId">The identifier out of the address.</param>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    public async Task<IActionResult> OnPostConfirmDeleteAsync(string documentId, CancellationToken cancellationToken)
    {
        var id = TryParseDocumentId(documentId);
        if (id is null)
        {
            return NotFound();
        }

        if (!TryParseSectionId(SectionId, out var sectionId) || ETagValue is null)
        {
            return BadRequest();
        }

        var result = await _documents.DeleteSectionAsync(id, sectionId, new ETag(ETagValue), cancellationToken);

        return await ApplyResultAsync(id, result, cancellationToken);
    }

    /// <summary>Carries out a move-up or move-down.</summary>
    /// <param name="documentId">The identifier out of the address.</param>
    /// <param name="offset">-1 for a move up, 1 for a move down.</param>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    /// <remarks>
    /// The new order is built from <see cref="SectionOrder"/>, the snapshot the
    /// form carried, and not from a document read again here: reading again
    /// would always succeed against itself and the very conflict this page
    /// exists to catch would never be seen.
    /// </remarks>
    private async Task<IActionResult> MoveAsync(string documentId, int offset, CancellationToken cancellationToken)
    {
        var id = TryParseDocumentId(documentId);
        if (id is null)
        {
            return NotFound();
        }

        if (!TryParseSectionId(SectionId, out var sectionId) || ETagValue is null || SectionOrder is null)
        {
            return BadRequest();
        }

        var order = SectionOrder;
        var index = order.FindIndex(value => string.Equals(value, sectionId.Value, StringComparison.Ordinal));
        var swapIndex = index + offset;

        if (index < 0 || swapIndex < 0 || swapIndex >= order.Count)
        {
            return BadRequest();
        }

        var reordered = new List<string>(order);
        (reordered[index], reordered[swapIndex]) = (reordered[swapIndex], reordered[index]);

        List<SectionIdentifier> orderedIds;
        try
        {
            orderedIds = [.. reordered.Select(value => new SectionIdentifier(value))];
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }

        var result = await _documents.ReorderSectionsAsync(id, orderedIds, new ETag(ETagValue), cancellationToken);

        return await ApplyResultAsync(id, result, cancellationToken);
    }

    /// <summary>Reloads the outline to show the message beside an empty heading, whichever form it came from.</summary>
    private async Task<IActionResult> ShowValidationErrorAsync(
        DocumentIdentifier id,
        CancellationToken cancellationToken,
        bool isNewHeading)
    {
        var loaded = await _documents.LoadDocumentAsync(id, cancellationToken);
        if (loaded is not DocumentResult.Success success)
        {
            return NotFound();
        }

        Apply(success);

        if (isNewHeading)
        {
            NewHeadingError = EmptyHeadingMessage;
        }
        else
        {
            RenameError = EmptyHeadingMessage;
        }

        return Page();
    }

    /// <summary>Turns the outcome of a change into the next response.</summary>
    /// <remarks>
    /// <see cref="DocumentResult.Conflict"/>, <see cref="DocumentResult.SectionNotFound"/>
    /// and <see cref="DocumentResult.OrderDoesNotMatchSections"/> are shown the
    /// same way: the document moved on since this page was loaded, so it is
    /// read again and shown with <see cref="ConflictMessage"/> instead of the
    /// form silently overwriting whatever is there now or the visitor meeting
    /// an error page. Whatever the visitor typed stays where it was bound,
    /// because nothing here clears <see cref="NewHeading"/> or
    /// <see cref="RenameHeading"/>.
    /// </remarks>
    private async Task<IActionResult> ApplyResultAsync(
        DocumentIdentifier id,
        DocumentResult result,
        CancellationToken cancellationToken)
    {
        if (result is DocumentResult.Success)
        {
            return Redirect($"/dokumente/{id.Value}/gliederung");
        }

        if (result is DocumentResult.DocumentNotFound)
        {
            return NotFound();
        }

        var loaded = await _documents.LoadDocumentAsync(id, cancellationToken);
        if (loaded is not DocumentResult.Success success)
        {
            return NotFound();
        }

        Apply(success);
        Message = ConflictMessage;

        return Page();
    }

    /// <summary>Takes over what a loaded document shows on the page.</summary>
    private void Apply(DocumentResult.Success success)
    {
        DocumentId = success.Document.Id.Value;
        Title = success.Document.Title;
        StateLabel = DocumentStateLabel.Of(success.Document.State);
        Sections = success.Document.Sections;
        ETag = success.ETag.Value;
    }

    /// <summary>Parses the identifier out of the address, refusing anything <see cref="DocumentIdentifier"/> would.</summary>
    /// <returns>The identifier, or <see langword="null"/> for a value that names no document this application could ever have written.</returns>
    private static DocumentIdentifier? TryParseDocumentId(string documentId)
    {
        try
        {
            return new DocumentIdentifier(documentId);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Parses a section identifier out of a hidden field.</summary>
    /// <returns>Whether the value was a well-formed identifier.</returns>
    private static bool TryParseSectionId(string? value, out SectionIdentifier sectionId)
    {
        if (value is not null)
        {
            try
            {
                sectionId = new SectionIdentifier(value);

                return true;
            }
            catch (ArgumentException)
            {
            }
        }

        sectionId = null!;

        return false;
    }
}
