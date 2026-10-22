// Records safe audit warnings for tests without adding a logging dependency to Core.
using ReviewMyDoc.Core.Audit;
using ReviewMyDoc.Core.Documents;

namespace ReviewMyDoc.Tests.Audit;

internal sealed class RecordingAuditFailures : IAuditFailureReporter
{
    public List<(DocumentIdentifier DocumentId, AuditEventKind Kind, string FailureType)> Entries { get; } = [];

    public void Report(DocumentIdentifier documentId, AuditEventKind kind, string failureType) =>
        Entries.Add((documentId, kind, failureType));
}
