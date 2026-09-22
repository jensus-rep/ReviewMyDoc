// The check page of the rate limiting. This task sets the limiters up, but the
// sign-in that will ask the login limiter belongs to the following task, so
// without this page the behaviour that matters most, a refusal answered inside
// the frame of the page and not as an empty response, could only be asserted
// about and not shown. The page asks the real login limiter, so what it shows is
// what the sign-in will inherit, not an imitation of it.

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReviewMyDoc.Web.Security;

namespace ReviewMyDoc.Web.Pages.Pruefung;

/// <summary>
/// Shows what a request refused by a rate limiter looks like. Reachable in
/// development only; see <see cref="ProbePagesConvention"/>.
/// </summary>
public sealed class RatenbegrenzungModel : PageModel
{
    /// <summary>What the page says when an attempt was counted and allowed.</summary>
    public const string AcceptedMessage = "Der Versuch wurde angenommen.";

    /// <summary>What the page says when the limiter refused the attempt.</summary>
    public const string RefusedMessage =
        "Zu viele Versuche. Bitte warten Sie eine Minute und versuchen Sie es erneut.";

    private readonly PartitionedRateLimiter<HttpContext> _rateLimiter;

    /// <summary>Takes the named limiter of the sign-in.</summary>
    /// <param name="rateLimiter">
    /// The limiter registered under <see cref="LoginRateLimit.Name"/>. Named and
    /// not resolved by type: a page says which limiter it means, so it cannot
    /// quietly end up spending the budget of another one.
    /// </param>
    public RatenbegrenzungModel(
        [FromKeyedServices(LoginRateLimit.Name)] PartitionedRateLimiter<HttpContext> rateLimiter) =>
        _rateLimiter = rateLimiter;

    /// <summary>
    /// The text the visitor typed. Bound so that it can be given back, which is
    /// the point of the whole exercise: a refusal must not cost anybody their
    /// input.
    /// </summary>
    [BindProperty]
    public string? Note { get; set; }

    /// <summary>The sentence shown above the form, or nothing before the first attempt.</summary>
    public string? Message { get; private set; }

    /// <summary>Whether the last attempt was refused, so the page can mark the field.</summary>
    public bool WasRefused { get; private set; }

    /// <summary>Shows the empty form.</summary>
    public void OnGet()
    {
        // A GET costs nothing and is deliberately not counted; see
        // LoginRateLimit.PartitionOf.
    }

    /// <summary>Spends one permit of the limiter and reports what happened.</summary>
    /// <param name="cancellationToken">Cancels the wait if the visitor goes away.</param>
    /// <returns>
    /// The page. With status 200 when the attempt was allowed, with status 429
    /// and <c>Retry-After</c> when it was refused. In both cases the page, never
    /// an empty response.
    /// </returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // The limit first, before anything else the attempt would cost. A
        // refused attempt must not reach a password hash, the storage or a
        // provider that charges for it.
        using var lease = await _rateLimiter.AcquireAsync(HttpContext, cancellationToken: cancellationToken);

        if (!lease.IsAcquired)
        {
            RateLimitRefusal.Answer(Response, lease, RateLimitPartitions.Window);

            WasRefused = true;
            Message = RefusedMessage;

            // Page() keeps the status set above, so the visitor sees the frame,
            // the message and their own text while the server answers 429.
            return Page();
        }

        Message = AcceptedMessage;

        return Page();
    }
}
