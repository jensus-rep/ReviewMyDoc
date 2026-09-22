// What a read of a single entry returned: either the content together with the
// version stamp a later write has to send back, or the plain statement that
// there is no such entry. A missing entry is an ordinary answer of this store -
// a document without feedback simply has no feedback entries - and therefore a
// result value and not an exception.

namespace ReviewMyDoc.Core.Storage;

/// <summary>The outcome of reading one entry.</summary>
/// <remarks>
/// Serves assurance 1 of <c>docs/Datenmodell.md</c>: content and version stamp
/// arrive together, which is what lets the caller write the entry back with
/// exactly the version it had read. The two cases are separate types, so the
/// compiler and not a convention keeps a caller from using content that is not
/// there.
/// </remarks>
public abstract record ObjectReadResult
{
    /// <summary>
    /// Keeps the set of cases closed: only the two cases declared here exist,
    /// so a caller can switch over them exhaustively.
    /// </summary>
    private protected ObjectReadResult()
    {
    }

    /// <summary>The entry exists.</summary>
    /// <param name="Content">The content of the entry as text.</param>
    /// <param name="ETag">
    /// The version at the moment of the read. Handing it to
    /// <see cref="WriteCondition.MustMatch(ETag)"/> is how assurance 1 of
    /// <c>docs/Datenmodell.md</c> is kept.
    /// </param>
    public sealed record Found(string Content, ETag ETag) : ObjectReadResult;

    /// <summary>
    /// There is no entry under that path. An expected answer and not a failure:
    /// the caller decides whether it means an empty list, a fresh aggregate or a
    /// message to the user.
    /// </summary>
    public sealed record NotFound : ObjectReadResult;
}
