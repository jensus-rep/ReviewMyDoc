// The three states a document moves through. They are an enumeration and not a
// string so that no third spelling of "InReview" can reach a file, and they sit
// in a file of their own because the states outlive every service that reads
// them.

namespace ReviewMyDoc.Core.Documents;

/// <summary>Where a document stands between being written and being released.</summary>
/// <remarks>
/// <para>
/// The names are the ones <c>docs/Datenmodell.md</c> writes into the field
/// <c>state</c> of <c>document.json</c>, so the value in the file and the value
/// in the code are the same word. German labels for the user interface are
/// mapped in exactly one place, as <c>docs/Konventionen.md</c>, section Sprache,
/// requires - and that place is not here, because the domain does not render
/// anything.
/// </para>
/// <para>
/// The transitions are not part of this aggregate yet. A document carries its
/// state; moving it to <see cref="InReview"/> happens when a review order is
/// sent and to <see cref="Approved"/> when every order is accepted, and both of
/// those depend on aggregates that do not exist yet. Assurance 5 of
/// <c>docs/Datenmodell.md</c> is the rule they will have to keep.
/// </para>
/// </remarks>
public enum DocumentState
{
    /// <summary>The owner is writing; nothing has been sent out.</summary>
    Draft,

    /// <summary>At least one review order is out and not yet settled.</summary>
    InReview,

    /// <summary>Every review order is accepted or revoked and the document is released.</summary>
    Approved,
}
