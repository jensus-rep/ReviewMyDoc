// The name of one document. It is a type of its own so that a document
// identifier cannot be handed to a parameter that wants a section identifier,
// which is the mistake that would otherwise build a path to an entry that does
// not exist and only show up when a document could not be read.

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// Names one document, as <c>documents/{documentId}/</c> of
/// <c>docs/Datenmodell.md</c> uses it.
/// </summary>
/// <remarks>
/// The value is checked when it is made, so everything downstream - above all
/// the paths in <see cref="DocumentPaths"/> - can rely on it fitting what the
/// object store accepts. See <see cref="IdentifierText"/> for the alphabet and
/// the reason behind it.
/// </remarks>
public sealed record DocumentIdentifier
{
    /// <summary>Takes an identifier that already exists, from a file or from a link.</summary>
    /// <param name="value">The text of the identifier.</param>
    /// <exception cref="ArgumentNullException">No value was handed over.</exception>
    /// <exception cref="ArgumentException">The value is not an identifier of the model.</exception>
    public DocumentIdentifier(string value) => Value = IdentifierText.Validate(value, nameof(value));

    /// <summary>The text of the identifier, lower case and URL safe.</summary>
    public string Value { get; }

    /// <summary>Draws the identifier of a document that is being created.</summary>
    /// <returns>A fresh identifier that no document is expected to carry.</returns>
    public static DocumentIdentifier Draw() => new(IdentifierText.Draw());

    /// <summary>Returns the text of the identifier, so log and test output stay readable.</summary>
    public override string ToString() => Value;
}
