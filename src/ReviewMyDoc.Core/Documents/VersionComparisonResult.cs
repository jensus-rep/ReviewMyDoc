// What DocumentService.FindChangedSectionsAsync returned. A closed set of cases
// beside DocumentResult rather than a case added to it, because comparing a
// document against a frozen state answers a different question than changing
// the document does and carries a different payload - a list of section
// identifiers, not a document.

namespace ReviewMyDoc.Core.Documents;

/// <summary>The outcome of comparing a document's current sections against one of its frozen states.</summary>
/// <remarks>
/// The cases are closed, so a page can switch over them exhaustively. As with
/// <see cref="DocumentResult"/>, nothing here is an exception: a document or a
/// version that is not there is an ordinary answer, for example a stale link to
/// a state that was never frozen.
/// </remarks>
public abstract record VersionComparisonResult
{
    /// <summary>
    /// Keeps the set of cases closed: only the cases declared here exist, so a
    /// caller can switch over them exhaustively.
    /// </summary>
    private protected VersionComparisonResult()
    {
    }

    /// <summary>The comparison could be made.</summary>
    /// <param name="ChangedSectionIds">
    /// Every section of the document's current outline whose text differs from -
    /// or was not yet part of - the frozen state. Empty if the reviewed state and
    /// today's text agree everywhere.
    /// </param>
    /// <remarks>
    /// A section removed from the outline since the freeze is not listed here:
    /// there is no current text of it to differ from anything, and
    /// <c>docs/Konzept.md</c> only ever asks whether today's text of a section
    /// has moved on from what was reviewed.
    /// </remarks>
    public sealed record Success(IReadOnlyList<SectionIdentifier> ChangedSectionIds) : VersionComparisonResult;

    /// <summary>There is no such document.</summary>
    public sealed record DocumentNotFound : VersionComparisonResult;

    /// <summary>This document was never frozen at that version.</summary>
    public sealed record VersionNotFound : VersionComparisonResult;
}
