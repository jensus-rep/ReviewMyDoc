// The configuration of the storage: which of the two implementations the
// application uses and what each of them needs. It is one file so that the
// answer to "where do my documents go" can be read in one place, and so that
// every key has a name in code that matches the one in appsettings.json.
// Described for the operator in docs/Betrieb.md.

namespace ReviewMyDoc.Infrastructure.Storage;

/// <summary>
/// Everything under the configuration section <c>Storage</c>.
/// </summary>
/// <remarks>
/// Bound from <c>appsettings.json</c>, from environment variables and, locally,
/// from <c>dotnet user-secrets</c>, as <c>docs/Konventionen.md</c>, section
/// Code, prescribes. In Azure the same keys are App Settings with <c>__</c> as
/// the separator, for example <c>Storage__Blob__ServiceUri</c>.
/// </remarks>
public sealed class StorageOptions
{
    /// <summary>The name of the section these options are bound from.</summary>
    public const string SectionName = "Storage";

    /// <summary>Which implementation of the store the application uses.</summary>
    public StorageProvider Provider { get; set; } = StorageProvider.Auto;

    /// <summary>What the local directory store needs.</summary>
    public DirectoryStorageOptions Directory { get; set; } = new();

    /// <summary>What the Azure store needs.</summary>
    public BlobStorageOptions Blob { get; set; } = new();
}

/// <summary>Which implementation of the store the application uses.</summary>
/// <remarks>
/// The choice is configuration and not a compile time switch, so one build runs
/// on a developer machine and in Azure and the difference is an App Setting.
/// </remarks>
public enum StorageProvider
{
    /// <summary>
    /// Azure if a connection is configured, the local directory otherwise. This
    /// is the default, and it is what lets a fresh clone start and work without
    /// anybody configuring anything.
    /// </summary>
    Auto,

    /// <summary>Always the local directory, even if a connection is configured.</summary>
    Directory,

    /// <summary>
    /// Always Azure. Without a configured connection the store cannot be built,
    /// and that is reported rather than quietly answered with the local
    /// directory: whoever names this value means it, and documents written to a
    /// directory nobody backs up would be worse than a clear failure.
    /// </summary>
    Blob,
}

/// <summary>What the local directory store needs.</summary>
public sealed class DirectoryStorageOptions
{
    /// <summary>
    /// The directory that holds the entries. A relative path is resolved
    /// against the working directory of the application.
    /// </summary>
    public string RootPath { get; set; } = "App_Data/storage";
}

/// <summary>What the Azure store needs.</summary>
/// <remarks>
/// Two ways in, and they are not equal.
/// <see cref="ServiceUri"/> is the one for Azure: it carries no secret, and the
/// application signs in with its managed identity.
/// <see cref="ConnectionString"/> is for a developer machine and for an
/// emulator, it does carry a secret and therefore never stands in
/// <c>appsettings.json</c> with a value. If both are set,
/// <see cref="ServiceUri"/> wins, so a connection string left over from an
/// experiment cannot pull a deployed application off its managed identity.
/// </remarks>
public sealed class BlobStorageOptions
{
    /// <summary>
    /// The address of the blob service, for example
    /// <c>https://mystorageaccount.blob.core.windows.net</c>. Set in Azure;
    /// empty means the application does not sign in this way.
    /// </summary>
    public string ServiceUri { get; set; } = string.Empty;

    /// <summary>
    /// The connection string of a storage account or of an emulator. A secret,
    /// so it belongs in <c>dotnet user-secrets</c> or in an environment
    /// variable and never in the repository.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>The one container that holds every entry of the application.</summary>
    public string ContainerName { get; set; } = "reviewmydoc";

    /// <summary>
    /// The client id of the user assigned managed identity, if the application
    /// uses one. Empty means the system assigned identity, which is the normal
    /// case and the one <c>docs/Betrieb.md</c> describes.
    /// </summary>
    public string ManagedIdentityClientId { get; set; } = string.Empty;

    /// <summary>
    /// Whether a connection to Azure is configured at all. This is the question
    /// <see cref="StorageProvider.Auto"/> asks, and a container without an
    /// address to reach it is no connection, which is why the name counts too.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ContainerName)
        && (!string.IsNullOrWhiteSpace(ServiceUri) || !string.IsNullOrWhiteSpace(ConnectionString));
}
