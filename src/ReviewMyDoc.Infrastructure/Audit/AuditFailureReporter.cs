// Audit writes are best effort after a committed change. Log only structured
// identifiers; storage exception messages can contain credentials or personal data.
using Microsoft.Extensions.Logging;
using ReviewMyDoc.Core.Audit;
using ReviewMyDoc.Core.Documents;

namespace ReviewMyDoc.Infrastructure.Audit;

/// <summary>Reports missing audit entries to the application log.</summary>
public sealed class AuditFailureReporter(ILogger<AuditFailureReporter> logger) : IAuditFailureReporter
{
    /// <inheritdoc />
    public void Report(DocumentIdentifier documentId, AuditEventKind kind, string failureType) =>
        logger.LogWarning("Audit event {Kind} for document {DocumentId} could not be appended ({FailureType}).",
            kind, documentId.Value, failureType);
}
