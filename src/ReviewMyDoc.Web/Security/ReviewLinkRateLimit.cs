// The limiter policy of the redemption of a review link. A reviewer has no
// account; the token in the address is the whole proof, so the only way in is to
// present a token, and the only way to find one without being given it is to
// guess. This limiter is what makes guessing hopeless in practice, next to the
// length of the token itself.

using System.Threading.RateLimiting;

namespace ReviewMyDoc.Web.Security;

/// <summary>
/// Partition and limit of the named limiter that guards the redemption of a
/// review link.
/// </summary>
public static class ReviewLinkRateLimit
{
    /// <summary>
    /// The name of the limiter: the key it is registered under and the key a
    /// page asks for with <c>[FromKeyedServices(ReviewLinkRateLimit.Name)]</c>.
    /// </summary>
    public const string Name = "review-link";

    /// <summary>
    /// Redemptions per minute and client address when
    /// <c>RateLimits:ReviewLink:PermitLimit</c> names no usable value.
    /// </summary>
    /// <remarks>
    /// Twenty, and deliberately higher than the sign-in. A reviewer opens the
    /// link, reloads it, opens it again from a second mail and may sit behind
    /// the same company address as three colleagues, so a tight limit here locks
    /// out honest reviewers, which is a real cost. Twenty a minute still leaves
    /// guessing a token nowhere near feasible.
    /// </remarks>
    public const int DefaultPermitLimit = 20;

    /// <summary>Decides the partition a request is counted in.</summary>
    /// <param name="context">The request the limiter is asked about.</param>
    /// <returns>The partition the request is counted in.</returns>
    /// <remarks>
    /// <para>
    /// Every method counts, unlike the sign-in. A review link is redeemed by
    /// opening it, so the guess is the GET, and exempting GET would exempt the
    /// attack.
    /// </para>
    /// <para>
    /// Counted per hashed client address and not per token: guessing means many
    /// different tokens from one place, and a partition per token would give
    /// every single guess a budget of its own.
    /// </para>
    /// </remarks>
    public static RateLimitPartition<string> PartitionOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return RateLimitPartitions.PerClientAddress(
            context,
            RateLimitPartitions.PermitLimitOf(context, options => options.ReviewLink, DefaultPermitLimit));
    }
}
