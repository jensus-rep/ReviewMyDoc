// The limits of the named rate limiters. They are configuration and not
// constants, because the right number depends on how the application is used
// and on what is happening to it: an operator under a wave of attempts has to be
// able to tighten a limit without a new build. The window is not configurable;
// see RateLimitWindow. Described for the operator in docs/Betrieb.md.

namespace ReviewMyDoc.Web.Security;

/// <summary>Everything under the configuration section <c>RateLimits</c>.</summary>
/// <remarks>
/// In Azure the same keys are App Settings with <c>__</c> as the separator, for
/// example <c>RateLimits__Login__PermitLimit</c>.
/// </remarks>
public sealed class RateLimitOptions
{
    /// <summary>The name of the section these options are bound from.</summary>
    public const string SectionName = "RateLimits";

    /// <summary>The limiter of the password sign-in of the owner.</summary>
    public NamedRateLimitOptions Login { get; set; } = new();

    /// <summary>The limiter of the redemption of a review link.</summary>
    public NamedRateLimitOptions ReviewLink { get; set; } = new();

    /// <summary>The limiter of the AI endpoints.</summary>
    public NamedRateLimitOptions Ai { get; set; } = new();
}

/// <summary>What one named limiter can be configured with.</summary>
public sealed class NamedRateLimitOptions
{
    /// <summary>
    /// How many requests one partition may spend per window.
    /// </summary>
    /// <remarks>
    /// Zero or less means "not configured": each limiter then falls back to its
    /// own documented default, so a section that is missing, empty or mistyped
    /// leaves the application protected instead of unprotected. That is the
    /// opposite of how a missing storage key behaves, and for a good reason: a
    /// missing store is a fault, a missing limit is an open door.
    /// </remarks>
    public int PermitLimit { get; set; }
}
