// Owns immutable snapshots and comparison with the current document.
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Documents;

/// <summary>Owns immutable snapshots and comparison with the current document.</summary>
internal sealed class DocumentVersionService(IDocumentStore store, TimeProvider clock) : DocumentOperations(store, clock)
{
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

}
