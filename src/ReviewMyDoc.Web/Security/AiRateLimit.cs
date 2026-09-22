// The limiter policy of the AI endpoints. This one does not guard a secret, it
// guards a bill: every call goes to an external provider and is paid for by
// tokens. A page in a loop, a tab left open or a reload that fires a request per
// keystroke would run up that bill without anybody noticing, and the key that
// pays for it belongs to the operator alone.

using System.Threading.RateLimiting;

namespace ReviewMyDoc.Web.Security;

/// <summary>
/// Partition and limit of the named limiter that guards the AI endpoints.
/// </summary>
public static class AiRateLimit
{
    /// <summary>
    /// The name of the limiter: the key it is registered under and the key an
    /// endpoint asks for with <c>[FromKeyedServices(AiRateLimit.Name)]</c>.
    /// </summary>
    public const string Name = "ai";

    /// <summary>
    /// Calls per minute and client address when <c>RateLimits:Ai:PermitLimit</c>
    /// names no usable value.
    /// </summary>
    /// <remarks>
    /// Twenty. One person writing on one document asks for help a few times a
    /// minute at most, so twenty is far above honest use and far below what a
    /// runaway page costs.
    /// </remarks>
    public const int DefaultPermitLimit = 20;

    /// <summary>Decides the partition a request is counted in.</summary>
    /// <param name="context">The request the limiter is asked about.</param>
    /// <returns>The partition the request is counted in.</returns>
    /// <remarks>
    /// Every method counts. Unlike the sign-in page there is nothing free to
    /// read here: an AI endpoint that answers at all has already called the
    /// provider.
    /// </remarks>
    public static RateLimitPartition<string> PartitionOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return RateLimitPartitions.PerClientAddress(
            context,
            RateLimitPartitions.PermitLimitOf(context, options => options.Ai, DefaultPermitLimit));
    }
}
