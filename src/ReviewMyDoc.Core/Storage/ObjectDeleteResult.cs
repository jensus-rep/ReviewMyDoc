// What a delete returned. It is an enumeration and not a pair of record types
// because neither case carries data, and a caller that only wants to know
// whether there was anything to delete should not have to pattern match for it.

namespace ReviewMyDoc.Core.Storage;

/// <summary>The outcome of deleting one entry.</summary>
/// <remarks>
/// Deleting something that is not there is not a failure: after a cancelled
/// operation a caller may clean up twice, and repeating it has to stay
/// harmless. Serves assurance 6 of <c>docs/Datenmodell.md</c> together with
/// <see cref="IObjectStore.DeleteAsync(string, System.Threading.CancellationToken)"/>,
/// which deletes exactly the named entry and never a prefix, so the feedback of
/// a deleted section stays readable.
/// </remarks>
public enum ObjectDeleteResult
{
    /// <summary>The entry existed and is gone now.</summary>
    Deleted,

    /// <summary>There was no entry under that path, and nothing was changed.</summary>
    NotFound,
}
