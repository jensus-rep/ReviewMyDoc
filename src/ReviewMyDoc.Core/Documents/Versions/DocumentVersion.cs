// The frozen state docs/Datenmodell.md describes at versions/{version}.json: the
// outline of a document AND the text of every section, together, exactly as they
// stood at the moment it was frozen. It exists because a review order must point
// at something that cannot move under the reviewer while they work on it - see
// docs/Konzept.md, section Zwei Festlegungen, die tragen - and an outline alone
// would not show a reviewer a single word of what they are meant to judge.

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// One frozen state of a document, exactly as <c>versions/{version}.json</c> of
/// <c>docs/Datenmodell.md</c> holds it.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="Document"/> this type carries no operation that changes it:
/// assurance 2 of <c>docs/Datenmodell.md</c> makes a frozen state unchangeable
/// once written, so there is nothing here but a constructor and a lookup. It is
/// built once, by <see cref="DocumentService.FreezeVersionAsync"/>, from a
/// document and the text of its sections as they stand at that moment, and
/// nothing else in this model is ever allowed to construct one from anywhere
/// but a file that was already written that way.
/// </para>
/// <para>
/// It repeats <see cref="Document.Title"/> and the outline rather than pointing
/// back at the document, because the document keeps changing after the freeze
/// and a version has to stay a complete, self-contained answer to "what did the
/// reviewer see" long after the document itself no longer looks like this.
/// </para>
/// </remarks>
public sealed class DocumentVersion
{
    /// <summary>Builds a frozen state from its parts, as a freeze or a file read produces them.</summary>
    /// <param name="documentId">Which document this is a frozen state of.</param>
    /// <param name="version">The version number, counted from one.</param>
    /// <param name="title">The title the document carried at the moment of freezing.</param>
    /// <param name="sections">The outline and the text of every section, in the order they were shown.</param>
    /// <param name="frozenAt">The moment this state was frozen.</param>
    /// <exception cref="ArgumentNullException">A reference that is needed is missing.</exception>
    /// <exception cref="ArgumentException">The title is empty, or two sections carry the same identifier.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The version is less than one.</exception>
    public DocumentVersion(
        DocumentIdentifier documentId,
        int version,
        string title,
        IReadOnlyList<FrozenSection> sections,
        DateTimeOffset frozenAt)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentNullException.ThrowIfNull(sections);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in sections)
        {
            ArgumentNullException.ThrowIfNull(section, nameof(sections));

            if (!seen.Add(section.Id.Value))
            {
                throw new ArgumentException(
                    $"The section '{section.Id}' appears twice; a section identifier names one section.",
                    nameof(sections));
            }
        }

        DocumentId = documentId;
        Version = version;
        Title = title.Trim();
        Sections = sections;
        FrozenAt = frozenAt;
    }

    /// <summary>Which document this is a frozen state of.</summary>
    public DocumentIdentifier DocumentId { get; }

    /// <summary>The version number, counted from one; the first frozen state of a document is <c>1</c>.</summary>
    public int Version { get; }

    /// <summary>The title the document carried at the moment of freezing.</summary>
    public string Title { get; }

    /// <summary>The outline and the text of every section, in the order they were shown when frozen.</summary>
    public IReadOnlyList<FrozenSection> Sections { get; }

    /// <summary>The moment this state was frozen.</summary>
    public DateTimeOffset FrozenAt { get; }

    /// <summary>Looks up one section of this frozen state.</summary>
    /// <param name="sectionId">The identifier to look for.</param>
    /// <returns>
    /// The section as it stood when frozen, or <see langword="null"/> if this
    /// state has no such section - the ordinary answer for a section that was
    /// added to the document after this state was frozen.
    /// </returns>
    public FrozenSection? FindSection(SectionIdentifier sectionId)
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
}

/// <summary>One section as a frozen state carries it: the outline entry together with the text.</summary>
/// <param name="Id">The identifier the section had at the moment of freezing; see <see cref="SectionIdentifier"/>.</param>
/// <param name="Heading">The heading it carried at that moment.</param>
/// <param name="Order">The position it held in the outline at that moment, counted from one.</param>
/// <param name="Text">
/// The Markdown text it carried at that moment - the reason a version exists at
/// all rather than the outline alone.
/// </param>
/// <remarks>
/// A record, because a frozen section is a value that never changes once
/// written: two frozen sections with equal fields are the same historical fact.
/// </remarks>
public sealed record FrozenSection(SectionIdentifier Id, string Heading, int Order, string Text);
