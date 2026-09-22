// Puts the Data Protection key ring into the service container and decides
// where its keys are kept. It lives here and not in Program.cs for the same
// reason AddObjectStore does: the web application must not have to name an
// Azure type, and the rule "Azure if the storage is Azure, the local directory
// otherwise" belongs in one place. See docs/Konventionen.md, section Code, and
// docs/Betrieb.md, section Data Protection.

using Azure.Extensions.AspNetCore.DataProtection.Blobs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Infrastructure.Security;

/// <summary>Registers the Data Protection key ring of the application.</summary>
/// <remarks>
/// <para>
/// The keys encrypt the sign-in cookie of the owner, the link session of a
/// reviewer and every antiforgery token. Without a key ring that outlives the
/// process, ASP.NET Core mints a fresh one at every start: every open session
/// ends and every form that was on screen fails with a token that nobody can
/// decrypt any more. In Azure it is worse than a restart, because a Web App is
/// restarted, moved and scaled without anybody asking, and a second instance
/// would not be able to read what the first one wrote.
/// </para>
/// <para>
/// That is why this registration exists, and it is why its assurance is
/// "survives a restart" rather than "is configured".
/// </para>
/// </remarks>
public static class DataProtectionServiceCollectionExtensions
{
    /// <summary>
    /// Binds the sections <c>Storage</c> and <c>DataProtection</c> and persists
    /// the key ring where the first of the two points.
    /// </summary>
    /// <param name="services">The container of the application.</param>
    /// <param name="configuration">The configuration the sections are read from.</param>
    /// <returns>
    /// The builder of Data Protection, so a caller could configure further; the
    /// composition root does not need to.
    /// </returns>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The configuration demands Azure without naming a connection. This stops
    /// the start of the application, and unlike the object store, which reports
    /// broken configuration only at the first access, that is the right moment
    /// here: the key ring is needed by the first request that renders a form, so
    /// an application that started would answer every single page with an error.
    /// A start that fails and names the key is the shorter way to the fix.
    /// </exception>
    public static IDataProtectionBuilder AddDataProtectionKeys(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
            ?? new StorageOptions();
        var options = configuration.GetSection(DataProtectionOptions.SectionName).Get<DataProtectionOptions>()
            ?? new DataProtectionOptions();

        var applicationName = string.IsNullOrWhiteSpace(options.ApplicationName)
            ? new DataProtectionOptions().ApplicationName
            : options.ApplicationName.Trim();

        var builder = services.AddDataProtection().SetApplicationName(applicationName);

        if (!StorageServiceCollectionExtensions.UsesBlob(storage))
        {
            return builder.PersistKeysToFileSystem(CreateKeyDirectory(options));
        }

        if (!storage.Blob.IsConfigured)
        {
            throw new InvalidOperationException(
                "The configuration demands Azure Blob Storage, but neither Storage:Blob:ServiceUri nor "
                + "Storage:Blob:ConnectionString names a connection, so the Data Protection keys have no "
                + "place to be kept. See docs/Betrieb.md.");
        }

        var blobName = string.IsNullOrWhiteSpace(options.BlobName)
            ? new DataProtectionOptions().BlobName
            : options.BlobName.Trim();

        // The same container client the documents are written with, built by the
        // same code: one connection, one managed identity, one role assignment.
        var blob = StorageServiceCollectionExtensions
            .CreateContainerClient(storage.Blob)
            .GetBlobClient(blobName);

        return builder.PersistKeysToAzureBlobStorage(blob);
    }

    /// <summary>Resolves and creates the directory the keys lie in.</summary>
    /// <remarks>
    /// Created here and not at the first write, so that a path nobody may write
    /// to is reported while the application starts rather than in the middle of
    /// a sign-in. A relative path is resolved against the working directory,
    /// exactly as <c>Storage:Directory:RootPath</c> is.
    /// </remarks>
    private static DirectoryInfo CreateKeyDirectory(DataProtectionOptions options)
    {
        var path = string.IsNullOrWhiteSpace(options.KeysPath)
            ? new DataProtectionOptions().KeysPath
            : options.KeysPath.Trim();

        return Directory.CreateDirectory(Path.GetFullPath(path));
    }
}
