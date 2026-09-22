// Puts the Markdown renderer into the service container. It lives here and not
// in Program.cs so that the web application does not have to name Markdig, the
// same reason AddObjectStore lives in Infrastructure.Storage rather than in the
// web project.

using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Markdown;

namespace ReviewMyDoc.Infrastructure.Markdown;

/// <summary>Registers the Markdown rendering of the application.</summary>
public static class MarkdownServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MarkdigMarkdownRenderer"/> as the implementation of
    /// <see cref="IMarkdownRenderer"/>.
    /// </summary>
    /// <param name="services">The container of the application.</param>
    /// <returns>The container, so calls can be chained.</returns>
    /// <remarks>
    /// A singleton, unlike the request-scoped services of this application: the
    /// renderer carries no state beyond the one immutable pipeline it is built
    /// with, and building that pipeline anew for every request would be pure
    /// waste.
    /// </remarks>
    public static IServiceCollection AddMarkdownRenderer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMarkdownRenderer, MarkdigMarkdownRenderer>();

        return services;
    }
}
