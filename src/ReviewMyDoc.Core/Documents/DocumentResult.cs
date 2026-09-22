// What an operation of DocumentService returned. Every outcome a user can
// produce is a case here and not an exception, because the page beside the form
// has to turn each of them into a sentence - above all the conflict, which is
// what two browser tabs on one document produce in the normal course of things.

using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Core.Documents;

/// <summary>The outcome of one operation on a document.</summary>
/// <remarks>
/// <para>
/// The cases are closed, so a page can switch over them and the compiler notices
/// when a case is added. What is not here is anything nobody planned for: a lost
/// connection or a broken file stays an exception and belongs to the error page,
/// as <c>docs/Datenmodell.md</c>, section Lokal und in Azure, lays down.
/// </para>
/// </remarks>
public abstract record DocumentResult
{
    /// <summary>
    /// Keeps the set of cases closed: only the cases declared here exist, so a
    /// caller can switch over them exhaustively.
    /// </summary>
    private protected DocumentResult()
    {
    }

    /// <summary>The operation took effect.</summary>
    /// <param name="Document">The document as it is now.</param>
    /// <param name="ETag">
    /// The version the entry carries now. The page renders it into the next form
    /// so the following change carries it back, which is how assurance 1 of
    /// <c>docs/Datenmodell.md</c> is kept without reading again.
    /// </param>
    public sealed record Success(Document Document, ETag ETag) : DocumentResult;

    /// <summary>
    /// There is no such document. A link can name one that was deleted, so this
    /// is an ordinary answer and not a failure.
    /// </summary>
    public sealed record DocumentNotFound : DocumentResult;

    /// <summary>The document has no section with this identifier.</summary>
    /// <param name="SectionId">The identifier that named nothing.</param>
    /// <remarks>
    /// Reached when a form names a section that a change somewhere else removed
    /// in the meantime, after the version still matched - two tabs where the one
    /// in front deleted a section and reloaded.
    /// </remarks>
    public sealed record SectionNotFound(SectionIdentifier SectionId) : DocumentResult;

    /// <summary>
    /// The new order is not the sections of this document: it leaves one out or
    /// names one twice.
    /// </summary>
    /// <remarks>
    /// Its own case and not a
    /// <see cref="SectionNotFound"/>, because nothing is missing that could be
    /// named - the list as a whole does not fit, and the page has to ask for the
    /// order again rather than point at one section.
    /// </remarks>
    public sealed record OrderDoesNotMatchSections : DocumentResult;

    /// <summary>
    /// The document changed after it was read, and nothing was written.
    /// </summary>
    /// <remarks>
    /// This is assurance 1 of <c>docs/Datenmodell.md</c> as the user meets it:
    /// the version handed back is no longer the current one, so the change was
    /// refused instead of overwriting the other one. The user interface says
    /// "inzwischen geändert" and offers the current state.
    /// </remarks>
    public sealed record Conflict : DocumentResult;
}
