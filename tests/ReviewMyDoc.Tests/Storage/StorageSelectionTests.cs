// Checks which of the two object stores a given configuration produces. The
// contract tests measure each store against the interface; this file measures
// the one decision that sits in front of both of them, because getting it wrong
// means documents land somewhere nobody looks.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Storage;

/// <summary>
/// Checks the choice between the local directory and Azure that
/// <see cref="StorageServiceCollectionExtensions.AddObjectStore"/> makes.
/// </summary>
/// <remarks>
/// The choice is configuration and not a compile time switch, which is what
/// these tests hold it to: the same build has to produce either store
/// depending on what the configuration says. No test here touches a network or
/// a storage account; building the client of the Azure SDK makes no request.
/// </remarks>
public sealed class StorageSelectionTests : IDisposable
{
    /// <summary>
    /// A directory of this test's own, so a store that really is built does not
    /// create <c>App_Data/</c> inside the repository.
    /// </summary>
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "reviewmydoc-tests",
        Guid.NewGuid().ToString("n"));

    // Without any configuration the application has to start and work, which is
    // what makes a fresh clone runnable. Answering with an exception here would
    // mean nobody can start the application before they have an Azure account.
    [Fact]
    public void Without_a_configured_connection_the_local_directory_is_used()
    {
        var store = Resolve(new Dictionary<string, string?>());

        Assert.IsType<DirectoryObjectStore>(store);
    }

    // The counterpart: a configured connection is meant to be used, and nothing
    // beyond it has to be set for that to happen.
    [Fact]
    public void A_configured_service_address_selects_azure()
    {
        var store = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Blob:ServiceUri"] = "https://example.blob.core.windows.net",
        });

        Assert.IsType<BlobObjectStore>(store);
    }

    // A connection string is the other way in, the one a developer machine and
    // an emulator use.
    [Fact]
    public void A_configured_connection_string_selects_azure()
    {
        var store = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Blob:ConnectionString"] = "UseDevelopmentStorage=true",
        });

        Assert.IsType<BlobObjectStore>(store);
    }

    // Naming the provider has to beat the presence of a connection, otherwise a
    // connection string left over from an experiment would decide where a
    // developer's documents go.
    [Fact]
    public void The_named_provider_wins_over_a_configured_connection()
    {
        var store = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "Directory",
            ["Storage:Blob:ServiceUri"] = "https://example.blob.core.windows.net",
        });

        Assert.IsType<DirectoryObjectStore>(store);
    }

    // Whoever names Azure means it. Answering that with the local directory
    // would write the documents of a deployed application into a container of
    // the web app that the next deployment throws away, and nobody would be
    // told.
    [Fact]
    public void Azure_without_a_connection_is_reported_and_not_answered_with_the_directory()
    {
        var exception = Assert.Throws<ObjectStoreException>(() => Resolve(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "Blob",
        }));

        Assert.Contains("Storage:Blob:ServiceUri", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Removes the directory a resolved local store may have created.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Builds the container the way the web application does and asks it for the
    /// store, so the test goes through the same registration and not past it.
    /// </summary>
    private IObjectStore Resolve(Dictionary<string, string?> settings)
    {
        settings["Storage:Directory:RootPath"] = _root;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddObjectStore(configuration);

        return services.BuildServiceProvider().GetRequiredService<IObjectStore>();
    }
}
