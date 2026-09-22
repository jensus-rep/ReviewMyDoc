// The configuration of the Data Protection key ring: where the keys are kept
// and under which application name. It is a file of its own, next to
// StorageOptions, because the keys are the second thing this application stores
// outside its own process and the operator has to be able to find the answer to
// "where are my keys" in one place. Described for the operator in
// docs/Betrieb.md.

namespace ReviewMyDoc.Infrastructure.Security;

/// <summary>
/// Everything under the configuration section <c>DataProtection</c>.
/// </summary>
/// <remarks>
/// <para>
/// Which of the two places the keys go to is <em>not</em> decided here. That is
/// <c>Storage:Provider</c>, exactly the setting that decides where the documents
/// go, so an operator configures one storage and gets one storage. These options
/// only say what each of the two places needs.
/// </para>
/// <para>
/// Bound from <c>appsettings.json</c>, from environment variables and, locally,
/// from <c>dotnet user-secrets</c>. In Azure the same keys are App Settings with
/// <c>__</c> as the separator, for example <c>DataProtection__BlobName</c>.
/// </para>
/// </remarks>
public sealed class DataProtectionOptions
{
    /// <summary>The name of the section these options are bound from.</summary>
    public const string SectionName = "DataProtection";

    /// <summary>
    /// The name the key ring is isolated under.
    /// </summary>
    /// <remarks>
    /// A fixed name, and that is the whole point of it. Without one, Data
    /// Protection derives the name from the content root path, so the same
    /// application in a second deployment slot, in a second container or simply
    /// in a different folder would mint a key ring of its own and could not read
    /// a cookie the other one wrote. A fixed name makes one application out of
    /// all of them.
    /// </remarks>
    public string ApplicationName { get; set; } = "ReviewMyDoc";

    /// <summary>
    /// The directory the keys lie in when the local directory is used. A
    /// relative path is resolved against the working directory of the
    /// application, like <c>Storage:Directory:RootPath</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately not inside <c>Storage:Directory:RootPath</c>. The keys are
    /// not a document: whoever copies, shares or clears the document folder must
    /// not carry the keys along by accident, and must not drop them by accident
    /// either.
    /// </remarks>
    public string KeysPath { get; set; } = "App_Data/keys";

    /// <summary>
    /// The blob inside <c>Storage:Blob:ContainerName</c> that holds the key ring
    /// when Azure is used.
    /// </summary>
    /// <remarks>
    /// The same container as the documents, on purpose: it is already private,
    /// already reached with the managed identity and already carries the role
    /// assignment, so the key ring needs no second container, no second right
    /// and no second step in docs/Betrieb.md.
    /// </remarks>
    public string BlobName { get; set; } = "data-protection/keys.xml";
}
