// Puts the three named rate limiters into the service container. They are
// services that a page asks itself and not policies of the RateLimiter
// middleware, and that is the whole point: the middleware answers a refusal
// before the page ever runs, so the visitor gets an empty 429 and loses what
// they typed. Asked as a service, the limiter is consulted at the moment an
// attempt really begins, and the page answers the refusal with its own frame and
// the values that were bound. Taken from Atelier, where this is written down and
// argued; docs/Konventionen.md, section Code, requires it here as well.

using System.Threading.RateLimiting;

namespace ReviewMyDoc.Web.Security;

/// <summary>Registers the named rate limiters of the application.</summary>
public static class RateLimitServiceCollectionExtensions
{
    /// <summary>
    /// Binds the section <c>RateLimits</c> and registers one
    /// <see cref="PartitionedRateLimiter{HttpContext}"/> per name.
    /// </summary>
    /// <param name="services">The container of the application.</param>
    /// <param name="configuration">The configuration the section is read from.</param>
    /// <returns>The container, so calls can be chained.</returns>
    /// <remarks>
    /// Singletons, and they have to be: a counter that lived as long as a
    /// request would count to one and never further. The container disposes
    /// them, and their replenishment timers with them, when the application
    /// stops. Each is registered under its own name, so a page names the limiter
    /// it means and cannot silently get another one.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    public static IServiceCollection AddRateLimits(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddKeyedSingleton(
            LoginRateLimit.Name,
            (_, _) => PartitionedRateLimiter.Create<HttpContext, string>(LoginRateLimit.PartitionOf));

        services.AddKeyedSingleton(
            ReviewLinkRateLimit.Name,
            (_, _) => PartitionedRateLimiter.Create<HttpContext, string>(ReviewLinkRateLimit.PartitionOf));

        services.AddKeyedSingleton(
            AiRateLimit.Name,
            (_, _) => PartitionedRateLimiter.Create<HttpContext, string>(AiRateLimit.PartitionOf));

        return services;
    }
}
