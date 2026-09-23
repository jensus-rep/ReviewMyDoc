// Owns splitting a section and compensating intermediate text writes.
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Documents;

/// <summary>Owns splitting a section and compensating intermediate text writes.</summary>
internal sealed class DocumentSplitService(IDocumentStore store, TimeProvider clock) : DocumentOperations(store, clock)
{
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

}
