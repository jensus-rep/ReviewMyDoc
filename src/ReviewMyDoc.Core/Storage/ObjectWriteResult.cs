// What a write returned: either the new version stamp of the entry, or the
// statement that the condition was not met. The second case is deliberately a
// value and not an exception, because it is expected: two people working on one
// document produce it in the normal course of things, and the user interface
// answers it with a message, not with an error page.

namespace ReviewMyDoc.Core.Storage;

/// <summary>The outcome of writing one entry.</summary>
/// <remarks>
/// Serves assurances 1 and 2 of <c>docs/Datenmodell.md</c>: a write whose
/// condition does not hold does not take effect and is reported, so nothing is
/// ever silently overwritten and a frozen version is never written twice.
/// </remarks>
public abstract record ObjectWriteResult
{
    /// <summary>
    /// Keeps the set of cases closed: only the two cases declared here exist,
    /// so a caller can switch over them exhaustively.
    /// </summary>
    private protected ObjectWriteResult()
    {
    }

    /// <summary>The write took effect.</summary>
    /// <param name="ETag">
    /// The version the entry carries now. A caller that goes on working with the
    /// entry uses it for its next write and so keeps assurance 1 of
    /// <c>docs/Datenmodell.md</c> without reading again.
    /// </param>
    public sealed record Written(ETag ETag) : ObjectWriteResult;

    /// <summary>
    /// The condition was not met and nothing was written. The entry either
    /// already existed where it had to be new, or it no longer carries the
    /// expected version because someone changed or deleted it in the meantime.
    /// These reasons are one case on purpose: no storage tells them apart in one
    /// request, and distinguishing them by asking again would only report a
    /// state that may already be stale. The caller reads the entry again and
    /// shows the change; assurance 1 of <c>docs/Datenmodell.md</c> calls for the
    /// message "inzwischen geändert" at this point.
    /// </summary>
    public sealed record Conflict : ObjectWriteResult;
}
