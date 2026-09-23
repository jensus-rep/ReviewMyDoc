// Owns additions, ordering, renaming and removal of outline entries.
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Documents;

/// <summary>Owns additions, ordering, renaming and removal of outline entries.</summary>
internal sealed class DocumentOutlineService(IDocumentStore store, TimeProvider clock) : DocumentOperations(store, clock)
{
    /// <summary>Appends a section with a heading and an empty text.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="heading">The heading of the new section.</param>
    /// <param name="expectedETag">The version the caller read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// Success with the document whose last section is the new one, or
    /// <see cref="DocumentResult.DocumentNotFound"/> or
    /// <see cref="DocumentResult.Conflict"/>.
    /// </returns>
    /// <exception cref="ArgumentException">The heading is empty.</exception>
    /// <remarks>
    /// <para>
    /// The text is written first and the entry in <c>document.json</c> second, in
    /// the same order as the delete works and for the same reason: an entry in
    /// the outline must never point at a text that is not there, while a text
    /// nothing points at harms nobody. See
    /// <see cref="DeleteSectionAsync"/> for the full argument.
    /// </para>
    /// <para>
    /// The new section has no text yet - this story gives a section a heading and
    /// a place, and the editor comes later - but the entry is created all the
    /// same, because an empty text is a state and not a missing entry.
    /// </para>
    /// </remarks>
    public async Task<DocumentResult> AddSectionAsync(
        DocumentIdentifier documentId,
        string heading,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        var (stored, refused) = await LoadForChangeAsync(documentId, expectedETag, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        var now = Now();
        var sectionId = SectionIdentifier.Draw();
        var changed = stored!.Document.AddSection(sectionId, heading, now);

        // MustNotExist and not Unconditional: should a drawn identifier ever
        // name an existing text, this refuses it instead of overwriting a
        // section of some other document state. It is reported as a conflict,
        // which is the honest answer - nothing was written and trying again
        // draws a different identifier.
        var text = await _store.WriteSectionTextAsync(
            documentId,
            sectionId,
            string.Empty,
            WriteCondition.MustNotExist,
            cancellationToken);

        if (text is not ObjectWriteResult.Written)
        {
            return new DocumentResult.Conflict();
        }

        var written = await _store.WriteAsync(
            changed,
            WriteCondition.MustMatch(expectedETag),
            cancellationToken);

        if (written is ObjectWriteResult.Written created)
        {
            return new DocumentResult.Success(changed, created.ETag);
        }

        // The outline was not written, so nothing points at the text that was
        // just created. The identifier was drawn a moment ago and is in no file,
        // so removing it can take nothing away from anybody. Should the removal
        // itself fail, the store is broken; that is not a conflict to be shown
        // beside a form and is left to travel up as the exception it is.
        await _store.DeleteSectionTextAsync(documentId, sectionId, cancellationToken);

        return new DocumentResult.Conflict();
    }

    /// <summary>Gives one section another heading.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="sectionId">Which section.</param>
    /// <param name="heading">The new heading.</param>
    /// <param name="expectedETag">The version the caller read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// Success, <see cref="DocumentResult.DocumentNotFound"/>,
    /// <see cref="DocumentResult.SectionNotFound"/> or
    /// <see cref="DocumentResult.Conflict"/>.
    /// </returns>
    /// <exception cref="ArgumentException">The heading is empty.</exception>
    /// <remarks>
    /// Touches <c>document.json</c> and nothing else: the heading lives there and
    /// not in the <c>.md</c> file, so renaming a section never opens its text.
    /// The identifier of the section stays as it is.
    /// </remarks>
    public async Task<DocumentResult> RenameSectionAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        string heading,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sectionId);

        var (stored, refused) = await LoadForChangeAsync(documentId, expectedETag, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        if (stored!.Document.FindSection(sectionId) is null)
        {
            return new DocumentResult.SectionNotFound(sectionId);
        }

        var changed = stored.Document.RenameSection(sectionId, heading, Now());

        return await WriteChangeAsync(changed, expectedETag, cancellationToken);
    }

    /// <summary>Puts the sections of a document in another order.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="orderedSectionIds">Every section of the document exactly once, in the new order.</param>
    /// <param name="expectedETag">The version the caller read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// Success, <see cref="DocumentResult.DocumentNotFound"/>,
    /// <see cref="DocumentResult.SectionNotFound"/> for an identifier that names
    /// nothing in this document, <see cref="DocumentResult.OrderDoesNotMatchSections"/>
    /// if the list as a whole does not fit, or <see cref="DocumentResult.Conflict"/>.
    /// </returns>
    /// <remarks>
    /// Not one identifier changes here. This is the operation the stable section
    /// identifier of <c>docs/Konzept.md</c> was invented for: whatever the owner
    /// does to the order, a piece of feedback written earlier still points at the
    /// section it was written on.
    /// </remarks>
    public async Task<DocumentResult> ReorderSectionsAsync(
        DocumentIdentifier documentId,
        IReadOnlyList<SectionIdentifier> orderedSectionIds,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedSectionIds);

        var (stored, refused) = await LoadForChangeAsync(documentId, expectedETag, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        var document = stored!.Document;
        foreach (var sectionId in orderedSectionIds)
        {
            ArgumentNullException.ThrowIfNull(sectionId, nameof(orderedSectionIds));

            if (document.FindSection(sectionId) is null)
            {
                return new DocumentResult.SectionNotFound(sectionId);
            }
        }

        // Every named section belongs to the document, so what is left to go
        // wrong is the shape of the list: one left out or one named twice.
        if (orderedSectionIds.Count != document.Sections.Count
            || orderedSectionIds.Select(sectionId => sectionId.Value).Distinct(StringComparer.Ordinal).Count()
                != orderedSectionIds.Count)
        {
            return new DocumentResult.OrderDoesNotMatchSections();
        }

        var changed = document.Reorder(orderedSectionIds, Now());

        return await WriteChangeAsync(changed, expectedETag, cancellationToken);
    }

    /// <summary>Removes one section with its text.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="sectionId">Which section.</param>
    /// <param name="expectedETag">The version the caller read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// Success, <see cref="DocumentResult.DocumentNotFound"/>,
    /// <see cref="DocumentResult.SectionNotFound"/> or
    /// <see cref="DocumentResult.Conflict"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>The order of the two writes, and why.</b> A section is two entries: its
    /// line in <c>document.json</c> and its <c>.md</c> file. They cannot be
    /// written at once - there is no transaction over two blobs - so one of them
    /// is first, and the choice decides what is left over if the machine stops in
    /// between.
    /// </para>
    /// <para>
    /// The entry goes first, under the version condition, and the text
    /// afterwards, without one. Assurance 1 of <c>docs/Datenmodell.md</c> names
    /// this order: which sections a document has is decided in
    /// <c>document.json</c>, and removing the orphaned text is cleanup and not a
    /// race, which is also why deleting twice has to stay harmless.
    /// </para>
    /// <para>
    /// <b>What is left over in the worst case:</b> a <c>.md</c> file that no
    /// entry names. Nothing reads it - every path to a section text is built from
    /// the outline - it takes up a few bytes, and the next delete of the same
    /// section, or a cleanup run, removes it. The other order would leave the
    /// opposite: a line in the outline whose text is gone, so the document would
    /// show a section that cannot be opened, and a review order could be given on
    /// it. That is a broken document, while an orphaned file is only untidy.
    /// </para>
    /// <para>
    /// <b>Should the cleanup fail</b>, the failure travels up as the exception it
    /// is instead of being swallowed: an <see cref="ObjectStoreException"/> means
    /// the store itself is not working, and reporting success then would hide it.
    /// The section is gone from the document all the same, because that write had
    /// already taken effect - which is the residue described above and not a half
    /// deleted section.
    /// </para>
    /// <para>
    /// The identifier of the deleted section is not reused, so assurance 6 of
    /// <c>docs/Datenmodell.md</c> holds: feedback written on it stays readable and
    /// can be marked as orphaned later.
    /// </para>
    /// </remarks>
    public async Task<DocumentResult> DeleteSectionAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sectionId);

        var (stored, refused) = await LoadForChangeAsync(documentId, expectedETag, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        if (stored!.Document.FindSection(sectionId) is null)
        {
            return new DocumentResult.SectionNotFound(sectionId);
        }

        var changed = stored.Document.RemoveSection(sectionId, Now());

        var written = await _store.WriteAsync(
            changed,
            WriteCondition.MustMatch(expectedETag),
            cancellationToken);

        if (written is not ObjectWriteResult.Written removed)
        {
            // Nothing was written, so the section is still in the document and
            // its text must stay exactly where it is.
            return new DocumentResult.Conflict();
        }

        await _store.DeleteSectionTextAsync(documentId, sectionId, cancellationToken);

        return new DocumentResult.Success(changed, removed.ETag);
    }

}
