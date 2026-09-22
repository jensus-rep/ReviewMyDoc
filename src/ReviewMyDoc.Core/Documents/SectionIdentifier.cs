// The name of one section, and the value everything later in this application
// hangs on: an assignment in a review order and a piece of feedback both point
// at a section by this identifier. It is a type of its own so it cannot be
// confused with the identifier of the document it belongs to.

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// Names one section, as <c>documents/{documentId}/sections/{sectionId}.md</c>
/// of <c>docs/Datenmodell.md</c> uses it.
/// </summary>
/// <remarks>
/// <para>
/// The identifier is given out once, when the section is created, and is never
/// changed again. Reordering the sections and renaming one leave it untouched,
/// which is what <c>docs/Konzept.md</c>, section Die vier Begriffe, means by a
/// stable identifier: feedback that was written on a section stays pointing at
/// that section however the outline is rearranged afterwards.
/// </para>
/// <para>
/// It also outlives the section itself. Assurance 6 of
/// <c>docs/Datenmodell.md</c> says identifiers do not die: feedback on a deleted
/// section stays readable and is marked as orphaned, which only works because
/// no later section is ever given the identifier of an earlier one - and a drawn
/// value never is.
/// </para>
/// </remarks>
public sealed record SectionIdentifier
{
    /// <summary>Takes an identifier that already exists, from a file or from a form.</summary>
    /// <param name="value">The text of the identifier.</param>
    /// <exception cref="ArgumentNullException">No value was handed over.</exception>
    /// <exception cref="ArgumentException">The value is not an identifier of the model.</exception>
    public SectionIdentifier(string value) => Value = IdentifierText.Validate(value, nameof(value));

    /// <summary>The text of the identifier, lower case and URL safe.</summary>
    public string Value { get; }

    /// <summary>Draws the identifier of a section that is being created.</summary>
    /// <returns>A fresh identifier that no section is expected to carry.</returns>
    /// <remarks>
    /// This is the only place a section identifier comes into being. Everything
    /// else in the application passes it on unchanged.
    /// </remarks>
    public static SectionIdentifier Draw() => new(IdentifierText.Draw());

    /// <summary>Returns the text of the identifier, so log and test output stay readable.</summary>
    public override string ToString() => Value;
}
