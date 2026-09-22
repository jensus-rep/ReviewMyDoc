// What the three named limiters have in common: the length of the window and
// the way a fixed window per hashed client address is built. It stands here once
// so that the limiters differ only where they really differ, namely in what they
// count and how much of it they allow.

using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace ReviewMyDoc.Web.Security;

/// <summary>Shared pieces of the rate limiter policies.</summary>
public static class RateLimitPartitions
{
    /// <summary>
    /// The window every limiter counts in.
    /// </summary>
    /// <remarks>
    /// One minute, and not configurable. The number of attempts is what an
    /// operator may want to change; the length of the window is what the
    /// documented limits are read against, and two knobs that both change the
    /// same rate make the limits impossible to talk about. It is also the time
    /// a refused visitor is told to wait, and a minute is short enough to be no
    /// punishment and long enough to make guessing pointless.
    /// </remarks>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// One fixed window per hashed client address, with the given number of
    /// permits.
    /// </summary>
    /// <param name="context">The request the limiter is asked about.</param>
    /// <param name="permitLimit">Requests allowed per window and address.</param>
    /// <returns>The partition the request is counted in.</returns>
    /// <remarks>
    /// No queue. A refused request is answered at once with 429 and a page
    /// rather than held open, which would tie up a connection and leave the
    /// visitor looking at nothing.
    /// </remarks>
    public static RateLimitPartition<string> PerClientAddress(HttpContext context, int permitLimit)
    {
        ArgumentNullException.ThrowIfNull(context);

        return RateLimitPartition.GetFixedWindowLimiter(
            ClientAddressHash.PartitionOf(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = Window,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }

    /// <summary>
    /// Reads the configured limit of one named limiter, or its default.
    /// </summary>
    /// <param name="context">The request, which carries the services.</param>
    /// <param name="select">Picks the entry of this limiter out of the section.</param>
    /// <param name="fallback">The limit to use when none is configured.</param>
    /// <returns>The number of requests allowed per window.</returns>
    /// <remarks>
    /// Read per request and not once at startup, so that a changed App Setting
    /// takes effect on a restart of the application without a new build. The
    /// limiter of a known partition is built once per key, so a changed value
    /// reaches an address that is already being counted only after its window
    /// has passed.
    /// </remarks>
    public static int PermitLimitOf(
        HttpContext context,
        Func<RateLimitOptions, NamedRateLimitOptions> select,
        int fallback)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(select);

        var options = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        var configured = select(options).PermitLimit;

        return configured > 0 ? configured : fallback;
    }
}
