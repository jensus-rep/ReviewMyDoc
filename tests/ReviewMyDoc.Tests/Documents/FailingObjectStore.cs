// An object store that works normally except where a test asks it to fail. It
// exists for one question that cannot be asked otherwise: what a document looks
// like when a change that touches two entries gets through the first and not the
// second.

using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Tests.Documents;

/// <summary>
/// Passes every call on to a real store and fails the ones a test switched on.
/// </summary>
/// <remarks>
/// It wraps a real store instead of replacing it, so what survives a failure can
/// be read back afterwards through the healthy store on the same directory. The
/// failure it raises is an <see cref="ObjectStoreException"/>, which is what
/// <c>docs/Datenmodell.md</c>, section Lokal und in Azure, says an
/// implementation reports when something happens that no caller could have
/// planned for.
/// </remarks>
public sealed class FailingObjectStore : IObjectStore
{
    private readonly IObjectStore _inner;

    /// <summary>Wraps a store that works.</summary>
    /// <param name="inner">The store every call is passed on to.</param>
    public FailingObjectStore(IObjectStore inner) => _inner = inner;

    /// <summary>Whether <see cref="DeleteAsync"/> fails instead of deleting.</summary>
    public bool FailsToDelete { get; set; }

    /// <inheritdoc />
    public Task<ObjectReadResult> ReadAsync(string path, CancellationToken cancellationToken) =>
        _inner.ReadAsync(path, cancellationToken);

    /// <inheritdoc />
    public Task<ObjectWriteResult> WriteAsync(
        string path,
        string content,
        WriteCondition condition,
        CancellationToken cancellationToken) =>
        _inner.WriteAsync(path, content, condition, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken) =>
        _inner.ListAsync(prefix, cancellationToken);

    /// <inheritdoc />
    public Task AppendLineAsync(string path, string line, CancellationToken cancellationToken) =>
        _inner.AppendLineAsync(path, line, cancellationToken);

    /// <inheritdoc />
    public Task<ObjectDeleteResult> DeleteAsync(string path, CancellationToken cancellationToken) =>
        FailsToDelete
            ? throw new ObjectStoreException("The entry could not be deleted.")
            : _inner.DeleteAsync(path, cancellationToken);
}
