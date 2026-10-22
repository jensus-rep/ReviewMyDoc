// Keeps application logging outside the domain while making missing audit
// entries observable without exposing exception messages or credentials.
using ReviewMyDoc.Core.Documents;

namespace ReviewMyDoc.Core.Audit;

/// <summary>Reports a failed append using only safe identifiers and a failure type.</summary>
public interface IAuditFailureReporter
{
    /// <summary>Reports a gap after the domain change has already succeeded.</summary>
    void Report(DocumentIdentifier documentId, AuditEventKind kind, string failureType);
}
