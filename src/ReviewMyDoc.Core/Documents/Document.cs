// The aggregate of docs/Konzept.md: a title, an owner, a state, a version and an
// ordered list of sections - and no text of its own. Every change to the outline
// goes through this type, so the rules that hold the outline together are stated
// once here instead of in every service that touches a document.

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// One document with its outline, exactly as <c>document.json</c> of
/// <c>docs/Datenmodell.md</c> holds it.
/// </summary>
/// <remarks>
/// <para>
/// A class and not a record, although almost everything else in this model is a
/// record. An aggregate is the same document after it has been renamed, so it is
/// told apart by <see cref="Id"/> and not by its fields; a record would promise
/// a value equality that its list of sections could not keep anyway, because two
/// lists with equal entries are not equal references.
/// </para>
/// <para>
/// It is immutable all the same: every change returns a new document. That is
/// what lets a service compute the next state, hand it to the store and,
/// should the write come back as a conflict, simply drop it - nothing was
/// changed in place that would now have to be undone.
/// </para>
/// <para>
/// <b>Two records of the order, one authority.</b>
/// <c>docs/Datenmodell.md</c> writes the sections as a list and gives each one an
/// <c>order</c> as well. The list is the authority: <see cref="Order"/> is
/// renumbered from the position on every change, gapless and counted from one.
/// So a person who rescues a file by hand moves an entry and gets what they
/// meant, and the number stays what it is meant to be - a convenience for
/// whoever reads the file or another tool, never a second truth that could
/// disagree with the first.
/// </para>
/// </remarks>
public sealed class Document
{
    /// <summary>Builds a document from its parts, as a file read or a change produces them.</summary>
    /// <param name="id">The identifier of the document.</param>
    /// <param name="ownerId">Who the document belongs to.</param>
    /// <param name="title">The title, trimmed of surrounding whitespace.</param>
    /// <param name="state">Where the document stands.</param>
    /// <param name="version">How many times a state of it has been frozen; zero for a new document.</param>
    /// <param name="sections">
    /// The outline in the order it is shown. The positions decide
    /// <see cref="Section.Order"/>, whatever the entries carried before.
    /// </param>
    /// <param name="createdAt">When the document was created.</param>
    /// <param name="updatedAt">When the document last changed.</param>
    /// <exception cref="ArgumentNullException">A reference that is needed is missing.</exception>
    /// <exception cref="ArgumentException">
    /// The owner or the title is empty, or two sections carry the same
    /// identifier - which would make every later assignment ambiguous.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The version is negative or the state is not one of the three.</exception>
    public Document(
        DocumentIdentifier id,
        string ownerId,
        string title,
        DocumentState state,
        int version,
        IReadOnlyList<Section> sections,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        ArgumentNullException.ThrowIfNull(sections);

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "A document is in one of the three states of docs/Datenmodell.md.");
        }

        Id = id;
        OwnerId = ownerId.Trim();
        Title = title.Trim();
        State = state;
        Version = version;
        Sections = Renumber(sections);
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>The identifier of the document; it never changes.</summary>
    public DocumentIdentifier Id { get; }

    /// <summary>
    /// Who the document belongs to. The first version of the application has
    /// exactly one owner, and <c>docs/Konzept.md</c>, section Offene
    /// Entscheidungen, keeps the field anyway so that a second owner later costs
    /// no migration.
    /// </summary>
    public string OwnerId { get; }

    /// <summary>The title the owner gave the document.</summary>
    public string Title { get; }

    /// <summary>Where the document stands between being written and being released.</summary>
    public DocumentState State { get; }

    /// <summary>
    /// How many states of this document have been frozen. A new document carries
    /// zero, because <c>docs/Datenmodell.md</c> counts this up only when a state
    /// is frozen and not on every keystroke; the first frozen state will be
    /// <c>versions/1.json</c>.
    /// </summary>
    public int Version { get; }

    /// <summary>The outline in the order it is shown, renumbered from one.</summary>
    public IReadOnlyList<Section> Sections { get; }

    /// <summary>When the document was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When anything about the document or its outline last changed.</summary>
    public DateTimeOffset UpdatedAt { get; }

    /// <summary>Creates a document that has nothing in it yet.</summary>
    /// <param name="id">The drawn identifier of the new document.</param>
    /// <param name="ownerId">Who the document belongs to.</param>
    /// <param name="title">The title the owner gave it.</param>
    /// <param name="now">The moment it is created.</param>
    /// <returns>A document in state <see cref="DocumentState.Draft"/>, at version zero and without sections.</returns>
    public static Document Create(DocumentIdentifier id, string ownerId, string title, DateTimeOffset now) =>
        new(id, ownerId, title, DocumentState.Draft, version: 0, sections: [], createdAt: now, updatedAt: now);

    /// <summary>Looks up one section of this document.</summary>
    /// <param name="sectionId">The identifier to look for.</param>
    /// <returns>The section, or <see langword="null"/> if this document has no such section.</returns>
    /// <remarks>
    /// The one place that answers whether a section belongs to a document. A
    /// service asks here before it changes anything, so that an identifier from
    /// a form which names no section of this document becomes a message and not
    /// an exception.
    /// </remarks>
    public Section? FindSection(SectionIdentifier sectionId)
    {
        ArgumentNullException.ThrowIfNull(sectionId);

        foreach (var section in Sections)
        {
            if (section.Id == sectionId)
            {
                return section;
            }
        }

        return null;
    }

    /// <summary>Gives the document another title.</summary>
    /// <param name="title">The new title.</param>
    /// <param name="now">The moment of the change.</param>
    /// <returns>A document with the new title; the outline is untouched.</returns>
    /// <exception cref="ArgumentException">The title is empty or only whitespace.</exception>
    public Document Rename(string title, DateTimeOffset now) =>
        new(Id, OwnerId, title, State, Version, Sections, CreatedAt, now);

    /// <summary>Appends a section to the end of the outline.</summary>
    /// <param name="sectionId">The drawn identifier of the new section.</param>
    /// <param name="heading">The heading of the new section.</param>
    /// <param name="now">The moment of the change.</param>
    /// <returns>A document whose outline ends with the new section.</returns>
    /// <exception cref="ArgumentException">
    /// The heading is empty, or the document already has a section with this
    /// identifier - which a drawn identifier cannot be, so it would be a defect
    /// and not a case the user interface has to show.
    /// </exception>
    public Document AddSection(SectionIdentifier sectionId, string heading, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);

        if (FindSection(sectionId) is not null)
        {
            throw new ArgumentException("The document already has a section with this identifier.", nameof(sectionId));
        }

        var sections = new List<Section>(Sections)
        {
            new(sectionId, heading.Trim(), Sections.Count + 1, now),
        };

        return new Document(Id, OwnerId, Title, State, Version, sections, CreatedAt, now);
    }

    /// <summary>Gives one section another heading.</summary>
    /// <param name="sectionId">Which section to rename.</param>
    /// <param name="heading">The new heading.</param>
    /// <param name="now">The moment of the change.</param>
    /// <returns>A document in which that one section carries the new heading.</returns>
    /// <exception cref="ArgumentException">The heading is empty, or the document has no such section.</exception>
    /// <remarks>
    /// The identifier of the section is not touched. This is one half of what
    /// <c>docs/Konzept.md</c> promises with a stable identifier, and the reason
    /// feedback written before the rename still points at the right section.
    /// </remarks>
    public Document RenameSection(SectionIdentifier sectionId, string heading, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        RequireSection(sectionId);

        var sections = new List<Section>(Sections.Count);
        foreach (var section in Sections)
        {
            sections.Add(section.Id == sectionId
                ? section with { Heading = heading.Trim(), UpdatedAt = now }
                : section);
        }

        return new Document(Id, OwnerId, Title, State, Version, sections, CreatedAt, now);
    }

    /// <summary>Takes one section out of the outline.</summary>
    /// <param name="sectionId">Which section to remove.</param>
    /// <param name="now">The moment of the change.</param>
    /// <returns>A document without that section; the remaining ones keep their identifiers and are renumbered.</returns>
    /// <exception cref="ArgumentException">The document has no such section.</exception>
    /// <remarks>
    /// Only the entry goes; the text of the section is a separate entry of the
    /// object store and is removed by the service, in the order
    /// <see cref="DocumentService.DeleteSectionAsync"/> explains.
    /// </remarks>
    public Document RemoveSection(SectionIdentifier sectionId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        RequireSection(sectionId);

        var sections = new List<Section>(Sections.Count - 1);
        foreach (var section in Sections)
        {
            if (section.Id != sectionId)
            {
                sections.Add(section);
            }
        }

        return new Document(Id, OwnerId, Title, State, Version, sections, CreatedAt, now);
    }

    /// <summary>Puts the sections in another order.</summary>
    /// <param name="orderedSectionIds">
    /// Every section of this document exactly once, in the order it is to be
    /// shown from now on.
    /// </param>
    /// <param name="now">The moment of the change.</param>
    /// <returns>A document whose outline follows the given order.</returns>
    /// <exception cref="ArgumentException">
    /// The list is not exactly the sections of this document - it names one that
    /// does not belong here, leaves one out or repeats one.
    /// </exception>
    /// <remarks>
    /// The whole order is handed over rather than one move, because that makes
    /// the operation idempotent and independent of what the outline looked like
    /// when the page was rendered. Not a single identifier changes; only the
    /// positions, and with them <see cref="Section.Order"/>, do.
    /// </remarks>
    public Document Reorder(IReadOnlyList<SectionIdentifier> orderedSectionIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(orderedSectionIds);

        if (orderedSectionIds.Count != Sections.Count)
        {
            throw new ArgumentException(
                "A new order names every section of the document exactly once.",
                nameof(orderedSectionIds));
        }

        var sections = new List<Section>(Sections.Count);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sectionId in orderedSectionIds)
        {
            ArgumentNullException.ThrowIfNull(sectionId);

            var section = FindSection(sectionId);
            if (section is null || !taken.Add(sectionId.Value))
            {
                throw new ArgumentException(
                    "A new order names every section of the document exactly once.",
                    nameof(orderedSectionIds));
            }

            sections.Add(section);
        }

        return new Document(Id, OwnerId, Title, State, Version, sections, CreatedAt, now);
    }

    /// <summary>Refuses a section that does not belong to this document.</summary>
    /// <remarks>
    /// A guard and not a check a caller is meant to lean on: a service asks
    /// <see cref="FindSection"/> first and turns a miss into a result value, so
    /// reaching this throw means the section was looked up somewhere it does not
    /// belong.
    /// </remarks>
    private void RequireSection(SectionIdentifier sectionId)
    {
        if (FindSection(sectionId) is null)
        {
            throw new ArgumentException("The document has no section with this identifier.", nameof(sectionId));
        }
    }

    /// <summary>
    /// Numbers the sections from one in the order they were handed over, and
    /// refuses a list that carries the same identifier twice.
    /// </summary>
    /// <remarks>
    /// This is where the list becomes the authority over <see cref="Section.Order"/>.
    /// It runs in the constructor, so every document - one that was read from a
    /// file just as much as one a change produced - leaves this type numbered the
    /// same way, and no operation has to remember to renumber.
    /// </remarks>
    private static IReadOnlyList<Section> Renumber(IReadOnlyList<Section> sections)
    {
        var numbered = new Section[sections.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];
            ArgumentNullException.ThrowIfNull(section, nameof(sections));

            if (!seen.Add(section.Id.Value))
            {
                throw new ArgumentException(
                    $"The section '{section.Id}' appears twice; a section identifier names one section.",
                    nameof(sections));
            }

            var order = index + 1;
            numbered[index] = section.Order == order ? section : section with { Order = order };
        }

        return Array.AsReadOnly(numbered);
    }
}
