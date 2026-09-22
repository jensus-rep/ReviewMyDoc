// The single door to persistence. Every service of this application reads and
// writes through it, and the two implementations in ReviewMyDoc.Infrastructure
// - Azure Blob Storage and a local directory - are interchangeable because this
// file, and not the calling service, says what the operations guarantee.

namespace ReviewMyDoc.Core.Storage;

/// <summary>
/// Reads and writes the entries of the one container described in
/// <c>docs/Datenmodell.md</c>. It knows entries, paths, versions and text -
/// nothing about documents, reviews or feedback.
/// </summary>
/// <remarks>
/// <para>
/// Paths are exactly the ones listed in <c>docs/Datenmodell.md</c>, for example
/// <c>documents/{documentId}/document.json</c>: segments separated by a forward
/// slash, without a leading slash, relative to the one container. They are
/// compared as ordinal strings and are therefore case sensitive. The store puts
/// no meaning into a path; building paths is the business of exactly one place
/// per aggregate, as laid down in <c>docs/Konventionen.md</c>.
/// </para>
/// <para>
/// Content travels as text and is stored as UTF-8 without a byte order mark.
/// The translation to and from JSON or Markdown is the business of the
/// services, so that the store stays free of domain types and every entry stays
/// readable by hand.
/// </para>
/// <para>
/// Expected outcomes are result values: a missing entry and a failed condition
/// are normal answers that the user interface turns into a message. Only what
/// nobody planned for - a lost connection, a missing permission, a broken
/// configuration - is reported as an exception, and no implementation lets a
/// type of its underlying storage escape through one.
/// </para>
/// </remarks>
public interface IObjectStore
{
    /// <summary>Reads one entry with its version.</summary>
    /// <param name="path">The path of the entry, as described in the remarks of this interface.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// <see cref="ObjectReadResult.Found"/> with content and version, or
    /// <see cref="ObjectReadResult.NotFound"/>.
    /// </returns>
    /// <remarks>
    /// Serves assurance 1 of <c>docs/Datenmodell.md</c>: it hands out content
    /// and version together, so the caller can give the version back on its next
    /// write and a change made in between becomes visible.
    /// </remarks>
    Task<ObjectReadResult> ReadAsync(string path, CancellationToken cancellationToken);

    /// <summary>Writes one entry if the condition allows it.</summary>
    /// <param name="path">The path of the entry, as described in the remarks of this interface.</param>
    /// <param name="content">The complete new content; entries are replaced whole, never patched.</param>
    /// <param name="condition">
    /// Which of the three intents the write carries: the entry must be new, it
    /// must still carry a known version, or neither is demanded.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// <see cref="ObjectWriteResult.Written"/> with the new version, or
    /// <see cref="ObjectWriteResult.Conflict"/> if the condition did not hold,
    /// in which case nothing was written.
    /// </returns>
    /// <remarks>
    /// Serves assurances 1 and 2 of <c>docs/Datenmodell.md</c>. The condition is
    /// checked where the write happens, so no other writer fits between the
    /// check and the write. With
    /// <see cref="WriteCondition.MustNotExist"/> this is what makes a frozen
    /// version unchangeable, with <see cref="WriteCondition.MustMatch(ETag)"/>
    /// it is what turns a concurrent change into a reported conflict instead of
    /// a silent overwrite.
    /// </remarks>
    Task<ObjectWriteResult> WriteAsync(
        string path,
        string content,
        WriteCondition condition,
        CancellationToken cancellationToken);

    /// <summary>Lists the paths of all entries that begin with a prefix.</summary>
    /// <param name="prefix">
    /// The beginning of the paths that are wanted, for example
    /// <c>documents/{documentId}/feedback/</c>. Matching is by characters and
    /// not by path segments, and deeper entries are included, so a prefix
    /// without a trailing slash also matches longer names.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The matching paths, sorted ascending as ordinal strings; an empty list if
    /// nothing matches. An empty result is not an error.
    /// </returns>
    /// <remarks>
    /// This is the only way the application asks a question across several
    /// entries, and it is the reason <c>docs/Datenmodell.md</c> can do without a
    /// database: every question starts either at one document or at a manageable
    /// number of them. The fixed order keeps both implementations comparable and
    /// spares the callers a sort of their own.
    /// </remarks>
    Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken);

    /// <summary>Appends one line to an entry and creates it if it is not there yet.</summary>
    /// <param name="path">The path of the entry, in practice <c>documents/{documentId}/audit.log</c>.</param>
    /// <param name="line">
    /// The line without a line break; the implementation adds the separator. A
    /// line that itself contains a line break would break the format of the log
    /// and is rejected.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once the line is appended.</returns>
    /// <remarks>
    /// <para>
    /// Serves the audit log of <c>docs/Datenmodell.md</c>, which is one JSON
    /// line per event. Appending is its own operation because reading, extending
    /// and writing back would lose an event as soon as two of them are recorded
    /// at the same moment, and because Azure Blob Storage appends without a
    /// version condition anyway. The entry being created on the first append
    /// spares every caller a check that would be a race.
    /// </para>
    /// <para>
    /// The store writes what it is handed. Assurance 7 of
    /// <c>docs/Datenmodell.md</c> - never a token in clear text, and no personal
    /// data beyond the reviewer identity - is therefore kept by the caller that
    /// builds the line.
    /// </para>
    /// </remarks>
    Task AppendLineAsync(string path, string line, CancellationToken cancellationToken);

    /// <summary>Deletes exactly one entry.</summary>
    /// <param name="path">The path of the entry, as described in the remarks of this interface.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// <see cref="ObjectDeleteResult.Deleted"/> or
    /// <see cref="ObjectDeleteResult.NotFound"/>; deleting twice is harmless.
    /// </returns>
    /// <remarks>
    /// Serves assurance 6 of <c>docs/Datenmodell.md</c>: it deletes the one
    /// named entry and never everything under a prefix, so the feedback of a
    /// deleted section stays readable and can be marked as orphaned. The delete
    /// carries no version condition, because which sections a document has is
    /// decided in <c>document.json</c>, and that entry is written under
    /// assurance 1; removing the text of a section that is no longer listed
    /// there is the cleanup afterwards.
    /// </remarks>
    Task<ObjectDeleteResult> DeleteAsync(string path, CancellationToken cancellationToken);
}
