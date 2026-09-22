// The one place that gives a DocumentState its German word, as
// docs/Konventionen.md, section Sprache, demands for every state value of the
// application: an English identifier in code, a German label in the interface,
// and the connection between the two written down exactly once.

using ReviewMyDoc.Core.Documents;

namespace ReviewMyDoc.Web.Pages.Dokumente;

/// <summary>Maps <see cref="DocumentState"/> to the word docs/Konzept.md uses for it.</summary>
internal static class DocumentStateLabel
{
    /// <summary>The German label shown in the interface for a document state.</summary>
    /// <param name="state">The state of a document.</param>
    /// <returns>The label, exactly as docs/Konzept.md, section Die vier Begriffe, names it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not one of the three states.</exception>
    internal static string Of(DocumentState state) => state switch
    {
        DocumentState.Draft => "Entwurf",
        DocumentState.InReview => "im Review",
        DocumentState.Approved => "freigegeben",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "A document is in one of the three states of docs/Datenmodell.md."),
    };
}
