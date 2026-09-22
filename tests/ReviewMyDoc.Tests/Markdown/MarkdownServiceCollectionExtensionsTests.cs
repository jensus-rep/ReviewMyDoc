// Checks the one line Program.cs relies on: that AddMarkdownRenderer puts a
// working IMarkdownRenderer into the container, the way AddObjectStore is
// checked in tests/ReviewMyDoc.Tests/Storage/StorageSelectionTests.cs.

using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Markdown;
using ReviewMyDoc.Infrastructure.Markdown;

namespace ReviewMyDoc.Tests.Markdown;

/// <summary>Checks <see cref="MarkdownServiceCollectionExtensions.AddMarkdownRenderer"/>.</summary>
public sealed class MarkdownServiceCollectionExtensionsTests
{
    [Fact]
    public void The_renderer_is_registered_and_resolvable()
    {
        var services = new ServiceCollection();
        services.AddMarkdownRenderer();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<MarkdigMarkdownRenderer>(provider.GetRequiredService<IMarkdownRenderer>());
    }

    // A singleton and not one instance per request: the renderer carries no
    // state beyond its immutable pipeline, and a second instance per request
    // would rebuild that pipeline for nothing.
    [Fact]
    public void The_renderer_is_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddMarkdownRenderer();

        using var provider = services.BuildServiceProvider();

        Assert.Same(
            provider.GetRequiredService<IMarkdownRenderer>(),
            provider.GetRequiredService<IMarkdownRenderer>());
    }
}
