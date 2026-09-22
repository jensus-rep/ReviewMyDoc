// The limiter policy of the password sign-in of the owner: how many attempts one
// client address may send per minute. ReviewMyDoc has exactly one account and no
// lockout per account, so this limiter is the only thing between a password hash
// and somebody who tries the whole dictionary against it.

using System.Threading.RateLimiting;

namespace ReviewMyDoc.Web.Security;

/// <summary>
/// Partition and limit of the named limiter that guards the sign-in of the
/// owner.
/// </summary>
public static class LoginRateLimit
{
    /// <summary>
    /// The name of the limiter: the key it is registered under and the key a
    /// page asks for with <c>[FromKeyedServices(LoginRateLimit.Name)]</c>.
    /// </summary>
    public const string Name = "login";

    /// <summary>
    /// Attempts per minute and client address when
    /// <c>RateLimits:Login:PermitLimit</c> names no usable value.
    /// </summary>
    /// <remarks>
    /// Ten. A person who mistypes their password does not need an eleventh try
    /// within the same minute, and somebody guessing gets ten guesses a minute
    /// against a hash that deliberately costs work, which is no attack any more.
    /// </remarks>
    public const int DefaultPermitLimit = 10;

    /// <summary>Decides the partition a request is counted in.</summary>
    /// <param name="context">The request the limiter is asked about.</param>
    /// <returns>The partition, or no limiter at all.</returns>
    /// <remarks>
    /// Only a POST counts. Opening or reloading the sign-in page costs the
    /// server neither a password hash nor anything else, and it must not use up
    /// the budget of the person who actually wants to sign in.
    /// </remarks>
    public static RateLimitPartition<string> PartitionOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter(nameof(HttpMethods.Get));
        }

        return RateLimitPartitions.PerClientAddress(
            context,
            RateLimitPartitions.PermitLimitOf(context, options => options.Login, DefaultPermitLimit));
    }
}
