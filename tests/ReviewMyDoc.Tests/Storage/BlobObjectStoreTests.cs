// Runs the contract of ObjectStoreContractTests against real Azure Blob
// Storage. Like its sister class for the directory store it adds no case of its
// own: what a store has to do is written down once, in the contract class. What
// it does add is the decision when to run at all, because this is the one test
// class of the project that needs something outside the machine it runs on.

using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Storage;

/// <summary>
/// Measures <see cref="BlobObjectStore"/> against everything
/// <see cref="ObjectStoreContractTests"/> demands of an object store.
/// </summary>
/// <remarks>
/// <para>
/// The tests run only if one of the two environment variables named below
/// carries a connection; without one they are skipped with that as the stated
/// reason and are not red, as <c>docs/Konventionen.md</c>, section Tests, asks
/// for. Red would be wrong: a developer without a storage account has not
/// broken anything, and a suite that is red by default is a suite nobody reads.
/// Skipped with a reason, on the other hand, is visible in every run and says
/// what is missing. How the variables are set stands in
/// <c>docs/Betrieb.md</c>.
/// </para>
/// <para>
/// Every test gets a container of its own, named after a fresh identifier and
/// removed afterwards. A shared container would not do: the contract works with
/// fixed paths such as <c>documents/d7kq2fr/document.json</c>, so two tests
/// running at the same time, or two runs from different worktrees against one
/// storage account, would write over each other. The container is the only
/// boundary that separates them completely.
/// </para>
/// </remarks>
public sealed class BlobObjectStoreTests : ObjectStoreContractTests, IDisposable
{
    /// <summary>Carries the connection string of a storage account or of an emulator.</summary>
    private const string ConnectionStringVariable = "REVIEWMYDOC_TEST_BLOB_CONNECTIONSTRING";

    /// <summary>
    /// Carries the address of a blob service that is reached with the sign-in of
    /// the machine, which is the way that needs no secret anywhere.
    /// </summary>
    private const string ServiceUriVariable = "REVIEWMYDOC_TEST_BLOB_SERVICEURI";

    /// <summary>Why a run without a connection skips instead of failing.</summary>
    private const string SkipReason =
        "Kein Blob Storage konfiguriert: Diese Tests laufen nur, wenn "
        + ConnectionStringVariable + " oder " + ServiceUriVariable
        + " gesetzt ist. Siehe docs/Betrieb.md, Abschnitt Tests gegen echtes Blob Storage.";

    private BlobContainerClient? _container;

    /// <inheritdoc />
    protected override IObjectStore CreateStore()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        var serviceUri = Environment.GetEnvironmentVariable(ServiceUriVariable);

        // Skipping happens here rather than in a fixture because the contract
        // class owns the test methods and must not be touched for this. Every
        // one of its cases asks for a store, and every one of them is therefore
        // reported as skipped with the reason below.
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(connectionString) && string.IsNullOrWhiteSpace(serviceUri),
            SkipReason);

        var service = string.IsNullOrWhiteSpace(serviceUri)
            ? new BlobServiceClient(connectionString)
            : new BlobServiceClient(new Uri(serviceUri), new DefaultAzureCredential());

        // A container name is lower case letters and digits, which an
        // identifier in hexadecimal form is, and it may not begin with a digit
        // in every naming rule the operator may have, hence the letter in front.
        _container = service.GetBlobContainerClient("t" + Guid.NewGuid().ToString("n"));
        _container.CreateIfNotExists();

        return new BlobObjectStore(_container);
    }

    /// <summary>Removes the container of this test.</summary>
    /// <remarks>
    /// A container that cannot be removed does not turn a green test red: the
    /// test has then already shown what it had to show, and a leftover container
    /// carries a name nobody uses twice. It is worth removing all the same,
    /// because these containers cost money as long as they exist.
    /// </remarks>
    public void Dispose()
    {
        try
        {
            _container?.DeleteIfExists();
        }
        catch (RequestFailedException)
        {
        }
        catch (AuthenticationFailedException)
        {
        }
    }
}
