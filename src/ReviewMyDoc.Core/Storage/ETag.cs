// The version stamp that a read hands out and a conditional write hands back.
// It is a type of its own so a stamp cannot be mistaken for a path or for
// content, and so the single rule both stores have to agree on - the value is
// opaque and compared ordinally - is written down in exactly one place.

namespace ReviewMyDoc.Core.Storage;

/// <summary>
/// The version of a stored entry at the moment it was read.
/// </summary>
/// <remarks>
/// Serves assurance 1 of <c>docs/Datenmodell.md</c>: a write against an
/// existing entry carries the stamp that was valid when it was read, so a
/// change made in between is reported as a conflict instead of being silently
/// overwritten. The value is opaque; in Azure it is the ETag of the blob, in
/// the local directory a hash of the content. Callers never parse it, they only
/// carry it from a read to the next write.
/// </remarks>
public sealed record ETag
{
    /// <summary>Wraps the value an implementation of the store reported.</summary>
    /// <param name="value">The opaque value, neither empty nor only whitespace.</param>
    /// <exception cref="ArgumentException">
    /// The value is empty or consists only of whitespace. An entry that exists
    /// always has a version, so a missing value is a defect of the
    /// implementation and not a case a caller has to handle.
    /// </exception>
    public ETag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An ETag needs a value.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// The opaque value. Two stamps are equal when their values are equal as
    /// ordinal strings; nothing else about the value is defined.
    /// </summary>
    public string Value { get; }

    /// <summary>Returns the opaque value, so log and test output stay readable.</summary>
    public override string ToString() => Value;
}
