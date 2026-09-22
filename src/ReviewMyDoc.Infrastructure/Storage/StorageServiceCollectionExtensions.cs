// Puts one of the two object stores into the service container and decides
// which one. It lives here and not in Program.cs so that the web application
// stays free of Azure types, as docs/Konventionen.md demands, and so that the
// rule "Azure if a connection is configured, the local directory otherwise" is
// written down once instead of in every host that uses this assembly.

using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Infrastructure.Storage;

/// <summary>Registers the storage of the application.</summary>
public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Binds the section <c>Storage</c> and registers the implementation of
    /// <see cref="IObjectStore"/> it names.
    /// </summary>
    /// <param name="services">The container of the application.</param>
    /// <param name="configuration">The configuration the section is read from.</param>
    /// <returns>The container, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// The store is a singleton. Both implementations may be used from several
    /// requests at once, and the client of the Azure SDK is meant to be shared:
    /// it pools its connections and caches the token of the managed identity,
    /// both of which one instance per request would throw away.
    /// </para>
    /// <para>
    /// The store is built the first time something asks for it and not while
    /// this method runs, which is what lets the application start with a
    /// configuration it cannot use. That is deliberate for the opposite case:
    /// without any configuration at all it starts on the local directory, so a
    /// fresh clone runs.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddObjectStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IObjectStore>(provider =>
            Create(provider.GetRequiredService<IOptions<StorageOptions>>().Value));

        return services;
    }

    /// <summary>Builds the store the configuration asks for.</summary>
    /// <exception cref="ObjectStoreException">
    /// Azure was demanded without a connection, or the configured address is
    /// not an address. Both are broken configuration, which the interface
    /// reports as this exception and not as a result value.
    /// </exception>
    internal static IObjectStore Create(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var useBlob = options.Provider switch
        {
            StorageProvider.Blob => true,
            StorageProvider.Directory => false,

            // Auto: whoever configures a connection wants it used, and whoever
            // configures none gets a store that works without one.
            _ => options.Blob.IsConfigured,
        };

        if (!useBlob)
        {
            return new DirectoryObjectStore(options.Directory.RootPath);
        }

        if (!options.Blob.IsConfigured)
        {
            throw new ObjectStoreException(
                "The configuration demands Azure Blob Storage, but neither Storage:Blob:ServiceUri nor "
                + "Storage:Blob:ConnectionString names a connection. See docs/Betrieb.md.");
        }

        return new BlobObjectStore(CreateContainerClient(options.Blob));
    }

    /// <summary>Builds the client of the one container, and signs in on the way.</summary>
    private static BlobContainerClient CreateContainerClient(BlobStorageOptions options)
    {
        var service = string.IsNullOrWhiteSpace(options.ServiceUri)
            ? new BlobServiceClient(options.ConnectionString)
            : new BlobServiceClient(ParseServiceUri(options.ServiceUri), CreateCredential(options));

        return service.GetBlobContainerClient(options.ContainerName.Trim());
    }

    /// <summary>Reads the address of the blob service.</summary>
    /// <remarks>
    /// A misspelled address would otherwise surface as a
    /// <see cref="UriFormatException"/> from somewhere inside the SDK, which
    /// says nothing about which key is wrong. The value itself is not put into
    /// the message: it is not a secret, but a log is no place for the name of a
    /// storage account either.
    /// </remarks>
    private static Uri ParseServiceUri(string serviceUri)
    {
        if (!Uri.TryCreate(serviceUri.Trim(), UriKind.Absolute, out var uri))
        {
            throw new ObjectStoreException(
                "Storage:Blob:ServiceUri is not an absolute address. See docs/Betrieb.md.");
        }

        return uri;
    }

    /// <summary>Chooses how the application proves who it is.</summary>
    /// <remarks>
    /// <see cref="DefaultAzureCredential"/> tries the managed identity in Azure
    /// and, on a developer machine, the sign-in of the Azure CLI or of Visual
    /// Studio. That is one class for both places and, more to the point, no key
    /// and no connection string anywhere. A user assigned identity has to be
    /// named, because an application that carries more than one identity cannot
    /// be asked to guess which one the container knows.
    /// </remarks>
    private static DefaultAzureCredential CreateCredential(BlobStorageOptions options) =>
        new(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = string.IsNullOrWhiteSpace(options.ManagedIdentityClientId)
                ? null
                : options.ManagedIdentityClientId.Trim(),
        });
}
