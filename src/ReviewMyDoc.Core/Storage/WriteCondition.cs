// The three intents a write can carry: the entry has to be new, the entry has
// to still carry a known version, or neither is demanded. They are one value
// instead of three methods because a service usually gets its intent from
// somewhere else - it reads, then writes back what it read - and because the
// place where the condition is turned into an If-Match or If-None-Match header
// stays a single one per implementation.

namespace ReviewMyDoc.Core.Storage;

/// <summary>The condition a write has to satisfy before it may take effect.</summary>
/// <remarks>
/// Serves assurances 1 and 2 of <c>docs/Datenmodell.md</c>. The condition is
/// checked by the store and not by the caller, so there is no gap between
/// checking and writing in which someone else could write.
/// </remarks>
public sealed record WriteCondition
{
    private WriteCondition(WriteConditionKind kind, ETag? expectedETag)
    {
        Kind = kind;
        ExpectedETag = expectedETag;
    }

    /// <summary>
    /// The entry must not exist yet; a write against an existing entry is a
    /// conflict. This is assurance 2 of <c>docs/Datenmodell.md</c>, under which
    /// <c>versions/{n}.json</c> is written once and never again, and it is also
    /// the right condition for every entry whose identity is drawn fresh, such
    /// as a review or a piece of feedback.
    /// </summary>
    public static WriteCondition MustNotExist { get; } = new(WriteConditionKind.MustNotExist, expectedETag: null);

    /// <summary>
    /// Nothing is demanded: the entry is written whether it exists or not. Only
    /// for entries on which no second writer is possible, for example while
    /// setting up a fresh installation. Using it on an entry that a user edits
    /// breaks assurance 1 of <c>docs/Datenmodell.md</c>, because a change made
    /// in between would be lost without anybody noticing.
    /// </summary>
    public static WriteCondition Unconditional { get; } = new(WriteConditionKind.Unconditional, expectedETag: null);

    /// <summary>
    /// The entry must exist and must still carry this version. This is
    /// assurance 1 of <c>docs/Datenmodell.md</c>: the stamp from the read comes
    /// back with the write, and if it no longer fits, the caller is told about
    /// the conflict instead of overwriting the other change.
    /// </summary>
    /// <param name="expectedETag">The version the read reported.</param>
    /// <returns>A condition that only this one version satisfies.</returns>
    /// <exception cref="ArgumentNullException">No version was handed over.</exception>
    public static WriteCondition MustMatch(ETag expectedETag)
    {
        ArgumentNullException.ThrowIfNull(expectedETag);

        return new WriteCondition(WriteConditionKind.MustMatch, expectedETag);
    }

    /// <summary>Which of the three intents this condition carries.</summary>
    public WriteConditionKind Kind { get; }

    /// <summary>
    /// The demanded version, set exactly for
    /// <see cref="WriteConditionKind.MustMatch"/> and otherwise absent.
    /// </summary>
    public ETag? ExpectedETag { get; }
}

/// <summary>The three intents a write can carry.</summary>
/// <remarks>
/// Named so an implementation can switch over them without reading the values
/// of <see cref="WriteCondition"/>; every value maps to one condition of the
/// underlying storage.
/// </remarks>
public enum WriteConditionKind
{
    /// <summary>Write whether the entry exists or not.</summary>
    Unconditional,

    /// <summary>Write only if the entry does not exist yet.</summary>
    MustNotExist,

    /// <summary>Write only if the entry still carries the expected version.</summary>
    MustMatch,
}
