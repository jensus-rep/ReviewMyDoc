// One entry of the outline: which section it is, what it is called and where it
// stands. The text of the section is deliberately not here - it lives in its own
// .md file - so that changing the outline and writing a section are two separate
// writes and therefore two separate conflicts.

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// One section as it appears in the list <c>sections</c> of
/// <c>document.json</c>.
/// </summary>
/// <param name="Id">
/// The identifier the section was given when it was created. It never changes;
/// see <see cref="SectionIdentifier"/>.
/// </param>
/// <param name="Heading">
/// The heading. It is kept here and not in the <c>.md</c> file so the outline
/// can be shown without loading a single section text, which is what
/// <c>docs/Datenmodell.md</c> gives as the reason for the split.
/// </param>
/// <param name="Order">
/// The position in the document, counted from one. It is derived from the
/// position in the list and written along with it; see
/// <see cref="Document"/> for why the list is the authority of the two.
/// </param>
/// <param name="UpdatedAt">
/// When this section last changed. Being moved is not a change of the section:
/// a section that is dragged to another place in the outline keeps this value,
/// because neither its heading nor its text is different afterwards. The
/// document records the move in its own <c>updatedAt</c>.
/// </param>
/// <remarks>
/// A record, because a section is a value: two sections with the same fields
/// are the same section entry, and nothing about one changes in place - every
/// change makes a new document.
/// </remarks>
public sealed record Section(SectionIdentifier Id, string Heading, int Order, DateTimeOffset UpdatedAt);
