// The status and the header of a request that a rate limiter refused. The page
// keeps rendering itself, with its frame and with what the visitor typed; this
// class only makes sure that the response says 429 and when to come back, and
// that it says it the same way everywhere. Taken from Atelier, where the reason
// is written down: docs/Konventionen.md, section Code, demands a refusal in the
// frame of the page and never an empty one.

using System.Globalization;
using System.Threading.RateLimiting;

namespace ReviewMyDoc.Web.Security;

/// <summary>Marks a response as refused by a rate limiter.</summary>
public static class RateLimitRefusal
{
    /// <summary>
    /// Sets status 429 and <c>Retry-After</c> in whole seconds.
    /// </summary>
    /// <param name="response">
    /// The response of the refused request. The page is rendered into it
    /// afterwards: returning <c>Page()</c> keeps the status that stands here, so
    /// the visitor gets the form back with their own input and the server still
    /// answers what really happened.
    /// </param>
    /// <param name="lease">
    /// The lease that was not acquired. A fixed window knows when it opens again
    /// and says so in its metadata.
    /// </param>
    /// <param name="fallbackRetryAfter">
    /// The time to name when the limiter knows none, as a concurrency limiter
    /// never does.
    /// </param>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    public static void Answer(HttpResponse response, RateLimitLease lease, TimeSpan fallbackRetryAfter)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(lease);

        var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var known)
            ? known
            : fallbackRetryAfter;

        // At least one second: "Retry-After: 0" would invite the next attempt at
        // once, and the header takes whole seconds only.
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
    }
}
