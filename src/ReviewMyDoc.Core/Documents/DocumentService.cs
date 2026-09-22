// Everything the owner does to a document and its outline: create it, load it,
// rename it, and add, rename, reorder and delete sections. It is the one place
// that decides in which order the entries of a document are written, which is
// what decides what a half-finished change leaves behind.

using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// Carries out the operations of <c>docs/Konzept.md</c>, section Dokument
/// anlegen und ausarbeiten, on the document aggregate.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every operation is optimistic.</b> The caller hands over the version it
/// read, the service reads the document, compares, applies the change and writes
/// with <see cref="WriteCondition.MustMatch(ETag)"/>. The comparison up front is
/// what makes the answer truthful - without it, a change would be computed
/// against a state the caller never saw, and a section deleted in the meantime
/// would be reported as missing when the honest answer is that the document
/// moved on. The condition on the write is what makes it safe: it closes the gap
/// between the read and the write, where no comparison of ours could reach.
/// </para>
/// <para>
/// <b>Section identifiers are drawn here and never again touched.</b> Renaming,
/// reordering and deleting other sections leave every identifier as it is, which
/// is the promise the review orders and the feedback of the later epics are
/// built on.
/// </para>
/// <para>
/// <b>The state is carried, not changed.</b> Nothing here moves a document to
/// <see cref="DocumentState.InReview"/> or <see cref="DocumentState.Approved"/>;
/// those transitions belong to the review orders and to the release, and they
/// have assurances 4 and 5 of <c>docs/Datenmodell.md</c> to keep.
/// </para>
/// </remarks>
public sealed class DocumentService
{
    private readonly IDocumentStore _store;
    private readonly TimeProvider _timeProvider;

    /// <summary>Builds the service over the store and the clock it works with.</summary>
    /// <param name="store">Where documents are read and written.</param>
    /// <param name="timeProvider">
    /// The clock that stamps <c>createdAt</c> and <c>updatedAt</c>. A parameter
    /// and not <see cref="DateTimeOffset.UtcNow"/>, so a test can state the
    /// moment and compare a written file down to the second.
    /// </param>
    /// <exception cref="ArgumentNullException">A dependency is missing.</exception>
    public DocumentService(IDocumentStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _timeProvider = timeProvider;
    }

    /// <summary>Creates a document with a title and no sections.</summary>
    /// <param name="ownerId">Who the document belongs to.</param>
    /// <param name="title">The title the owner gave it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// <see cref="DocumentResult.Success"/> with the new document, or
    /// <see cref="DocumentResult.Conflict"/> in the case described in the
    /// remarks.
    /// </returns>
    /// <exception cref="ArgumentException">The owner or the title is empty.</exception>
    /// <remarks>
    /// Written with <see cref="WriteCondition.MustNotExist"/>, so a drawn
    /// identifier that against all expectation already names a document cannot
    /// overwrite it. That is the only way this operation reports a conflict, and
    /// the answer to it is to try again, not to reload anything.
    /// </remarks>
    public async Task<DocumentResult> CreateDocumentAsync(
        string ownerId,
        string title,
        CancellationToken cancellationToken)
    {
        var document = Document.Create(DocumentIdentifier.Draw(), ownerId, title, Now());

        var written = await _store.WriteAsync(document, WriteCondition.MustNotExist, cancellationToken);

        return written is ObjectWriteResult.Written created
            ? new DocumentResult.Success(document, created.ETag)
            : new DocumentResult.Conflict();
    }

    /// <summary>Lists every document, newest change first.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every document there is, sorted by <see cref="Document.UpdatedAt"/> descending.</returns>
    /// <remarks>
    /// The sort is the one rule of this operation and the reason it is not left
    /// to the page: whoever asks for the list of documents wants the one that
    /// changed most recently first, and stating that once here means every page
    /// that shows the list agrees on it without repeating it.
    /// </remarks>
    public async Task<IReadOnlyList<Document>> ListDocumentsAsync(CancellationToken cancellationToken)
    {
        var documents = await _store.ListDocumentsAsync(cancellationToken);

        return [.. documents.OrderByDescending(document => document.UpdatedAt)];
    }

    /// <summary>Loads a document with the version needed for the next change.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// <see cref="DocumentResult.Success"/>, or
    /// <see cref="DocumentResult.DocumentNotFound"/>.
    /// </returns>
    public async Task<DocumentResult> LoadDocumentAsync(
        DocumentIdentifier documentId,
        CancellationToken cancellationToken)
    {
        var stored = await _store.ReadAsync(documentId, cancellationToken);

        return stored is null
            ? new DocumentResult.DocumentNotFound()
            : new DocumentResult.Success(stored.Document, stored.ETag);
    }

    /// <summary>Gives a document another title.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="title">The new title.</param>
    /// <param name="expectedETag">The version the caller read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, <see cref="DocumentResult.DocumentNotFound"/> or <see cref="DocumentResult.Conflict"/>.</returns>
    /// <exception cref="ArgumentException">The title is empty.</exception>
    public async Task<DocumentResult> RenameDocumentAsync(
        DocumentIdentifier documentId,
        string title,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        var (stored, refused) = await LoadForChangeAsync(documentId, expectedETag, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        var changed = stored!.Document.Rename(title, Now());

        return await WriteChangeAsync(changed, expectedETag, cancellationToken);
    }

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

    /// <summary>Splits one section into up to three at the edges of a marked stretch of its text.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="sectionId">Which section is being split.</param>
    /// <param name="markStart">
    /// The offset, in <see cref="char"/>s from the start of the section's text,
    /// where the mark begins.
    /// </param>
    /// <param name="markEnd">
    /// The offset where the mark ends. The marked text is
    /// <c>text[markStart..markEnd]</c>, so this is exclusive, and it must be
    /// strictly greater than <paramref name="markStart"/>.
    /// </param>
    /// <param name="heading">The heading of the section the mark becomes.</param>
    /// <param name="expectedETag">The version the caller read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// Success with the document whose outline has the split section's parts in
    /// its place, <see cref="DocumentResult.DocumentNotFound"/>,
    /// <see cref="DocumentResult.SectionNotFound"/>,
    /// <see cref="DocumentResult.InvalidSelection"/> if the mark does not name a
    /// real, non-empty stretch of the text, or <see cref="DocumentResult.Conflict"/>.
    /// </returns>
    /// <exception cref="ArgumentException">The heading is empty.</exception>
    /// <remarks>
    /// <para>
    /// This is <c>docs/Konzept.md</c>, section Dokument anlegen und ausarbeiten:
    /// "Aus der Markierung wird der Abschnitt." The text before the mark, the
    /// mark itself and the text after it can each be empty except the mark,
    /// which the caller refused as <see cref="DocumentResult.InvalidSelection"/>
    /// already were it empty - so this produces one, two or three sections, and
    /// never zero.
    /// </para>
    /// <para>
    /// <b>Which part keeps <paramref name="sectionId"/>, and why.</b>
    /// The identifier stays with the part that holds the first character of the
    /// original text: what stood before the mark, or the mark itself when the
    /// mark opens the section. <c>docs/Konzept.md</c> calls that "the first of
    /// the three", and where the first of the three is empty, the first one
    /// there actually is inherits it.
    /// </para>
    /// <para>
    /// The alternative would be to let the identifier end whenever no text
    /// stands before the mark, on the grounds that feedback pointing at it was
    /// written against the opening prose and should not silently reattach. That
    /// is the worse of the two. A piece of feedback belongs to a section, not to
    /// a particular stretch inside it, and dropping the identifier orphans that
    /// feedback while the prose it was written about is still in the document,
    /// only under a new name. A reference that leads nowhere although its
    /// subject is right there is harder to make sense of later than one that
    /// leads to a section grown shorter. Splitting therefore never orphans
    /// anything; only deleting a section does, and the owner is asked to confirm
    /// that.
    /// </para>
    /// <para>
    /// One case looks like an exception and is not: when the mark reaches from
    /// the very start of the text to the very end, nothing is split at all - the
    /// single result is the same section under
    /// <see cref="Document.RenameSection"/>, keeping <paramref name="sectionId"/>
    /// because no text has moved anywhere.
    /// </para>
    /// <para>
    /// <b>The order of the writes, and why.</b> A split touches up to three
    /// entries that cannot be written at once: the text of the part after the
    /// mark, the text of the mark itself, both under freshly drawn identifiers,
    /// and <c>document.json</c>. The two fresh texts are written first, under
    /// <see cref="WriteCondition.MustNotExist"/> and for the same reason as
    /// <see cref="AddSectionAsync"/> - nothing yet refers to a freshly drawn
    /// identifier, so a text under one is harmless to leave behind, while an
    /// entry in the outline that already names a text which is not there is not.
    /// Should either fail, or should <c>document.json</c> itself then be refused,
    /// the fresh texts already written are removed again, the same compensation
    /// <see cref="AddSectionAsync"/> and <see cref="FreezeVersionAsync"/> run for
    /// their own second write.
    /// </para>
    /// <para>
    /// The one entry this does not write first is the text that stays under
    /// <paramref name="sectionId"/>, shortened to the part that keeps it.
    /// That happens last, after <c>document.json</c> has already committed the
    /// split, and deliberately not before it. Shortening it first and writing
    /// the outline second would risk the failure
    /// <see cref="DeleteSectionAsync"/>'s remarks warn against in the other
    /// direction: the process stopping in between would leave a
    /// <c>document.json</c> that still lists one section with its original
    /// heading over a file that has already lost the marked and trailing text -
    /// content silently gone from a section the outline never said had changed.
    /// Writing the outline first and shortening the text second leaves the
    /// opposite residue in the same failure: <c>document.json</c> already
    /// describes the split correctly, and the one file that has not yet caught
    /// up merely holds more than it should - the marked and trailing text still
    /// sitting, unreachable through the outline, underneath the part that kept
    /// <paramref name="sectionId"/>. Surplus bytes in a file nobody's client
    /// reads past its own boundary once split are a rounding error, while
    /// content missing from a section that looks untouched is not - the same
    /// choice <see cref="DeleteSectionAsync"/> makes between an orphaned file and
    /// a broken reference, applied to a shrink instead of a removal.
    /// </para>
    /// </remarks>
    public async Task<DocumentResult> SplitSectionAsync(
        DocumentIdentifier documentId,
        SectionIdentifier sectionId,
        int markStart,
        int markEnd,
        string heading,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);

        var (stored, refused) = await LoadForChangeAsync(documentId, expectedETag, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        var document = stored!.Document;
        var section = document.FindSection(sectionId);
        if (section is null)
        {
            return new DocumentResult.SectionNotFound(sectionId);
        }

        var storedText = await _store.ReadSectionTextAsync(documentId, sectionId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Section '{sectionId}' is listed in document.json but has no text; the store is inconsistent.");
        var text = storedText.Content;

        if (markStart < 0 || markEnd > text.Length || markStart >= markEnd)
        {
            return new DocumentResult.InvalidSelection();
        }

        var now = Now();
        var beforeText = text[..markStart];
        var markedText = text[markStart..markEnd];
        var afterText = text[markEnd..];
        var trimmedHeading = heading.Trim();

        if (beforeText.Length == 0 && afterText.Length == 0)
        {
            // The mark spans the whole section: nothing stands before or after
            // it, so nothing splits off, and the identifier has nowhere else to
            // go but stay exactly where it is - see the remarks above.
            var renamed = document.RenameSection(sectionId, trimmedHeading, now);

            return await WriteChangeAsync(renamed, expectedETag, cancellationToken);
        }

        // The part that keeps sectionId is the one holding the first character
        // of the original text; every other part is written under a freshly
        // drawn identifier. See the remarks above.
        var markOpensTheSection = beforeText.Length == 0;
        var keptText = markOpensTheSection ? markedText : beforeText;
        var markedId = markOpensTheSection ? sectionId : SectionIdentifier.Draw();
        var afterId = afterText.Length > 0 ? SectionIdentifier.Draw() : null;

        var fresh = new List<SectionIdentifier>(2);
        if (!markOpensTheSection)
        {
            var markedWritten = await _store.WriteSectionTextAsync(
                documentId,
                markedId,
                markedText,
                WriteCondition.MustNotExist,
                cancellationToken);
            if (markedWritten is not ObjectWriteResult.Written)
            {
                return new DocumentResult.Conflict();
            }

            fresh.Add(markedId);
        }

        if (afterId is not null)
        {
            var afterWritten = await _store.WriteSectionTextAsync(
                documentId,
                afterId,
                afterText,
                WriteCondition.MustNotExist,
                cancellationToken);
            if (afterWritten is not ObjectWriteResult.Written)
            {
                await RemoveAsync(fresh);

                return new DocumentResult.Conflict();
            }

            fresh.Add(afterId);
        }

        var replacement = new List<Section>(3);
        if (!markOpensTheSection)
        {
            replacement.Add(section with { UpdatedAt = now });
        }

        replacement.Add(markOpensTheSection
            ? section with { Heading = trimmedHeading, UpdatedAt = now }
            : new Section(markedId, trimmedHeading, Order: 0, UpdatedAt: now));
        if (afterId is not null)
        {
            replacement.Add(new Section(afterId, section.Heading, Order: 0, UpdatedAt: now));
        }

        var changed = document.SplitSection(sectionId, replacement, now);
        var written = await _store.WriteAsync(changed, WriteCondition.MustMatch(expectedETag), cancellationToken);
        if (written is not ObjectWriteResult.Written updated)
        {
            // Nothing yet refers to the fresh texts, so removing them costs
            // nothing - the same compensation AddSectionAsync and
            // FreezeVersionAsync run when their own second write is refused. The
            // text under sectionId is untouched at this point and stays whole.
            await RemoveAsync(fresh);

            return new DocumentResult.Conflict();
        }

        {
            // See the remarks above: only now, with document.json already
            // committed to the split, is it safe to shorten the text that stayed
            // under sectionId. The condition is the stamp read at the top of this
            // call - nothing else in this model writes a section's text back
            // conditionally today, so it still applies, and the one write it
            // could ever refuse is not reported as a conflict of this operation:
            // the split itself already took effect in document.json, and the
            // residue described above is what is left instead.
            await _store.WriteSectionTextAsync(
                documentId,
                sectionId,
                keptText,
                WriteCondition.MustMatch(storedText.ETag),
                cancellationToken);
        }

        return new DocumentResult.Success(changed, updated.ETag);

        async Task RemoveAsync(IReadOnlyList<SectionIdentifier> written)
        {
            foreach (var identifier in written)
            {
                await _store.DeleteSectionTextAsync(documentId, identifier, cancellationToken);
            }
        }
    }

    /// <summary>Freezes the current state of a document as its next version.</summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="expectedETag">The version of <c>document.json</c> the caller read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// Success with the document whose version count reflects the freeze, or
    /// <see cref="DocumentResult.DocumentNotFound"/> or
    /// <see cref="DocumentResult.Conflict"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is why <c>docs/Datenmodell.md</c> has versions at all:
    /// <c>docs/Konzept.md</c>, section Review beauftragen, sends a reviewer to a
    /// state that must not move while they work on it. The frozen state carries
    /// the outline <b>and</b> the text of every section - an outline alone would
    /// tell a reviewer what the document is called and how it is divided, never
    /// what it says, which would make the whole idea of a fixed state pointless.
    /// </para>
    /// <para>
    /// <b>The order of the two writes, and why.</b> Freezing touches two entries
    /// that cannot be written at once: the new <c>versions/{n}.json</c> and the
    /// <c>version</c> field of <c>document.json</c>. The frozen state is written
    /// first, under <see cref="WriteCondition.MustNotExist"/> - assurance 2 of
    /// <c>docs/Datenmodell.md</c>, a version is never overwritten - and the
    /// counter in <c>document.json</c> second, under the caller's
    /// <paramref name="expectedETag"/>.
    /// </para>
    /// <para>
    /// Should that second write be refused - the ordinary case, somebody changed
    /// the document after it was read for this call - the version file just
    /// written is removed again before the conflict is reported. Nothing yet
    /// refers to it: no <c>document.json</c> ever named it and no review order
    /// can have been sent against it, so taking it back costs nothing. This is
    /// the same compensation <see cref="AddSectionAsync"/> runs when its own
    /// second write is refused, and for the same reason: it keeps a refused
    /// attempt free of any trace, so trying again after a reload finds the next
    /// version number still unclaimed instead of already taken by a leftover
    /// nobody can use.
    /// </para>
    /// <para>
    /// <b>What is left over in the one case that removal cannot reach:</b> the
    /// process stopping between the first write and that cleanup. Then
    /// <c>versions/{n}.json</c> survives while <c>document.json</c> still counts
    /// <c>n - 1</c>. That is the harmless half, in the sense
    /// <see cref="DeleteSectionAsync"/> means it: nothing refers to version
    /// <c>n</c> yet, and the file itself is a true, complete frozen state that
    /// costs nothing to leave lying around - the next freeze attempt reports a
    /// conflict instead of silently reusing the number, which is the safe
    /// failure and, if it is ever seen, the signal that the count needs setting
    /// by hand. The reverse order would leave the opposite: a
    /// <c>document.json</c> that already promises version <c>n</c> to a review
    /// order before the file that backs it up exists - a broken reference, and
    /// far worse than one unused file nobody has been sent a link to.
    /// </para>
    /// </remarks>
    public async Task<DocumentResult> FreezeVersionAsync(
        DocumentIdentifier documentId,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        var (stored, refused) = await LoadForChangeAsync(documentId, expectedETag, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        var document = stored!.Document;
        var now = Now();
        var nextVersionNumber = document.Version + 1;

        var sections = new List<FrozenSection>(document.Sections.Count);
        foreach (var section in document.Sections)
        {
            var text = await _store.ReadSectionTextAsync(documentId, section.Id, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Section '{section.Id}' is listed in document.json but has no text; the store is inconsistent.");

            sections.Add(new FrozenSection(section.Id, section.Heading, section.Order, text.Content));
        }

        var frozen = new DocumentVersion(documentId, nextVersionNumber, document.Title, sections, now);

        var versionWritten = await _store.WriteVersionAsync(frozen, WriteCondition.MustNotExist, cancellationToken);
        if (versionWritten is not ObjectWriteResult.Written)
        {
            // Sequential numbering makes this unreachable in the normal course of
            // things; reported as a conflict rather than trusted blindly, exactly
            // as assurance 2 of docs/Datenmodell.md asks for a version that
            // already exists.
            return new DocumentResult.Conflict();
        }

        var changed = document.Freeze(nextVersionNumber, now);

        var documentWritten = await _store.WriteAsync(
            changed,
            WriteCondition.MustMatch(expectedETag),
            cancellationToken);

        if (documentWritten is ObjectWriteResult.Written updated)
        {
            return new DocumentResult.Success(changed, updated.ETag);
        }

        // See the remarks above: nothing yet refers to this version, so removing
        // it costs nothing and keeps a refused attempt free of any trace.
        await _store.DeleteVersionAsync(documentId, nextVersionNumber, cancellationToken);

        return new DocumentResult.Conflict();
    }

    /// <summary>
    /// Tells which of a document's current sections have a text that differs
    /// from the one a frozen state carried.
    /// </summary>
    /// <param name="documentId">Which document.</param>
    /// <param name="version">Which frozen state to compare against.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// <see cref="VersionComparisonResult.Success"/> with the identifiers of
    /// every section whose current text differs from - or was not yet part of -
    /// the frozen state, or <see cref="VersionComparisonResult.DocumentNotFound"/>
    /// or <see cref="VersionComparisonResult.VersionNotFound"/>.
    /// </returns>
    /// <remarks>
    /// <c>docs/Konzept.md</c>, section Zwei Festlegungen, die tragen, promises
    /// that the interface says on both sides when today's text of a section has
    /// moved on from what a reviewer was sent. This is the one place that
    /// answers that question, so the editor and the review view can each render
    /// the same list their own way instead of comparing text themselves.
    /// </remarks>
    public async Task<VersionComparisonResult> FindChangedSectionsAsync(
        DocumentIdentifier documentId,
        int version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentId);

        var stored = await _store.ReadAsync(documentId, cancellationToken);
        if (stored is null)
        {
            return new VersionComparisonResult.DocumentNotFound();
        }

        var frozen = await _store.ReadVersionAsync(documentId, version, cancellationToken);
        if (frozen is null)
        {
            return new VersionComparisonResult.VersionNotFound();
        }

        var changedSectionIds = new List<SectionIdentifier>();
        foreach (var section in stored.Document.Sections)
        {
            var frozenSection = frozen.FindSection(section.Id);
            var currentText = (await _store.ReadSectionTextAsync(documentId, section.Id, cancellationToken))?.Content
                ?? string.Empty;

            if (frozenSection is null || !string.Equals(frozenSection.Text, currentText, StringComparison.Ordinal))
            {
                changedSectionIds.Add(section.Id);
            }
        }

        return new VersionComparisonResult.Success(changedSectionIds);
    }

    /// <summary>
    /// Reads the document for a change and refuses it if there is nothing to
    /// change or the caller is working from an older state.
    /// </summary>
    /// <returns>
    /// Either the document that was read, or the result the operation has to
    /// return instead; exactly one of the two is set.
    /// </returns>
    private async Task<(StoredDocument? Stored, DocumentResult? Refused)> LoadForChangeAsync(
        DocumentIdentifier documentId,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(expectedETag);

        var stored = await _store.ReadAsync(documentId, cancellationToken);
        if (stored is null)
        {
            return (null, new DocumentResult.DocumentNotFound());
        }

        if (stored.ETag != expectedETag)
        {
            return (null, new DocumentResult.Conflict());
        }

        return (stored, null);
    }

    /// <summary>Writes a changed document under the version the caller read.</summary>
    /// <remarks>
    /// The condition is the caller's version and not the one that was just read,
    /// although at this point they are equal: it is the version the user actually
    /// saw, and it is the one that has to hold at the moment of the write.
    /// </remarks>
    private async Task<DocumentResult> WriteChangeAsync(
        Document changed,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        var written = await _store.WriteAsync(
            changed,
            WriteCondition.MustMatch(expectedETag),
            cancellationToken);

        return written is ObjectWriteResult.Written updated
            ? new DocumentResult.Success(changed, updated.ETag)
            : new DocumentResult.Conflict();
    }

    /// <summary>
    /// The current moment in UTC, cut to whole seconds.
    /// </summary>
    /// <remarks>
    /// <c>docs/Datenmodell.md</c> writes its timestamps to the second, and
    /// nothing in this model tells finer moments apart: the outline is shown with
    /// a date, and the order of two changes is decided by the version of an
    /// entry and not by a clock. Cutting here rather than when the file is
    /// written keeps the document in memory and the document on disk the same
    /// down to the last digit.
    /// </remarks>
    private DateTimeOffset Now()
    {
        var now = _timeProvider.GetUtcNow();

        return new DateTimeOffset(now.UtcTicks - (now.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }
}
