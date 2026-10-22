// Injects failures at storage boundaries while retaining real persistence and
// measuring whether the audit log is ever rewritten instead of appended.
using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Tests.Audit;

internal sealed class AuditStoreProbe(IObjectStore inner) : IObjectStore
{
    public const string Secret = "NEVER-LOG-THIS-TOKEN-OR-HASH";
    public bool FailAppends { get; init; }
    public bool CancelAppends { get; init; }
    public bool RefuseDocumentWrites { get; init; }
    public int Appends;
    public int AuditRewrites;

    public Task<ObjectReadResult> ReadAsync(string path, CancellationToken ct) => inner.ReadAsync(path, ct);
    public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken ct) => inner.ListAsync(prefix, ct);
    public Task<ObjectDeleteResult> DeleteAsync(string path, CancellationToken ct)
    {
        if (path.EndsWith("/audit.log", StringComparison.Ordinal)) { Interlocked.Increment(ref AuditRewrites); }
        return inner.DeleteAsync(path, ct);
    }
    public Task<ObjectWriteResult> WriteAsync(string path, string content, WriteCondition condition, CancellationToken ct)
    {
        if (path.EndsWith("/audit.log", StringComparison.Ordinal)) { Interlocked.Increment(ref AuditRewrites); }
        return RefuseDocumentWrites && path.EndsWith("/document.json", StringComparison.Ordinal)
            ? Task.FromResult<ObjectWriteResult>(new ObjectWriteResult.Conflict()) : inner.WriteAsync(path, content, condition, ct);
    }
    public Task AppendLineAsync(string path, string line, CancellationToken ct)
    {
        Interlocked.Increment(ref Appends);
        if (CancelAppends) { throw new OperationCanceledException(Secret); }
        if (FailAppends) { throw new ObjectStoreException(Secret, new IOException(Secret)); }
        return inner.AppendLineAsync(path, line, ct);
    }
}
