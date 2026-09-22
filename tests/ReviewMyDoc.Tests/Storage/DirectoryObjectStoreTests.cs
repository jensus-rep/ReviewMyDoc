// Runs the contract of ObjectStoreContractTests against the local directory
// store. It adds no case of its own on purpose: what a store has to do is
// written down once, in the contract class, and this file only says what the
// store under test is built on and where its directory goes.

using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Storage;

namespace ReviewMyDoc.Tests.Storage;

/// <summary>
/// Measures <see cref="DirectoryObjectStore"/> against everything
/// <see cref="ObjectStoreContractTests"/> demands of an object store.
/// </summary>
/// <remarks>
/// Every test works in a directory of its own below the temporary directory of
/// the machine, named after a fresh identifier, and removes it afterwards.
/// xUnit builds a fresh instance of this class for each test, so no test sees
/// what another one wrote, and two runs from different worktrees on the same
/// machine cannot meet either - which is what
/// <c>docs/Konventionen.md</c>, section Tests, asks for.
/// </remarks>
public sealed class DirectoryObjectStoreTests : ObjectStoreContractTests, IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "reviewmydoc-tests",
        Guid.NewGuid().ToString("n"));

    /// <inheritdoc />
    protected override IObjectStore CreateStore() => new DirectoryObjectStore(_root);

    /// <summary>Removes the directory of this test.</summary>
    /// <remarks>
    /// A directory that cannot be removed does not turn a green test red: the
    /// test has then already shown what it had to show, and the temporary
    /// directory of the machine is cleaned up by the machine.
    /// </remarks>
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
}
