// A clock a test can set. The document service stamps createdAt and updatedAt
// from a TimeProvider precisely so a test can state the moment and then compare
// a written file down to the second instead of matching a pattern.

namespace ReviewMyDoc.Tests.Documents;

/// <summary>A <see cref="TimeProvider"/> that reports the moment it was told.</summary>
/// <remarks>
/// Written here rather than taken from a package: one overridden method is less
/// to carry than another dependency of the test project.
/// </remarks>
public sealed class FixedTimeProvider : TimeProvider
{
    /// <summary>Starts the clock at a moment.</summary>
    /// <param name="utcNow">What the clock reports until it is set again.</param>
    public FixedTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;

    /// <summary>The moment the clock reports; a test moves it to make time pass.</summary>
    public DateTimeOffset UtcNow { get; set; }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
