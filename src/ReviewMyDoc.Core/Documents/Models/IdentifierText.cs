// The alphabet every identifier of the model is drawn from and the one place
// that draws a new one. It exists because a document identifier and a section
// identifier are two types - so neither can be passed where the other belongs -
// while the alphabet, the length and the source of randomness behind them have
// to stay a single decision.

using System.Security.Cryptography;

namespace ReviewMyDoc.Core.Documents;

/// <summary>
/// Checks and draws the text of an identifier, as <c>docs/Datenmodell.md</c>,
/// section Ablage, describes it.
/// </summary>
/// <remarks>
/// <para>
/// Identifiers are URL safe random values and never running numbers. Random,
/// because a running number tells a visitor how many documents exist and in
/// which order they were made, and because two writers would have to agree on
/// the next number. URL safe and lower case, because an identifier becomes part
/// of a path of the object store, and that store refuses a path with an upper
/// case letter: blob names tell case apart and a Windows file system does not,
/// so a path that relied on case would mean two entries in one store and one in
/// the other.
/// </para>
/// <para>
/// The alphabet is the 26 lower case letters and the ten digits. The underscore
/// that <c>docs/Datenmodell.md</c> permits is not drawn: it adds no distinctions
/// that the other 36 characters do not already give, and an alphabet whose size
/// stays a round 36 keeps the draw plainly uniform. Twelve characters out of 36
/// are about 62 bits, which is far past the point where a collision could be
/// expected among the documents one owner writes - and a collision is not silent
/// anyway, because every entry is created with
/// <see cref="Storage.WriteCondition.MustNotExist"/> and would be reported as a
/// conflict rather than overwrite anything.
/// </para>
/// </remarks>
internal static class IdentifierText
{
    /// <summary>The characters an identifier is drawn from.</summary>
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>How many characters a drawn identifier carries.</summary>
    private const int DrawnLength = 12;

    /// <summary>Draws a new identifier.</summary>
    /// <returns>A fresh value over the alphabet described in the remarks of this class.</returns>
    /// <remarks>
    /// Drawn from <see cref="RandomNumberGenerator"/> and not from
    /// <see cref="System.Random"/>: an identifier reaches the outside world in a
    /// link, so it must not be guessable from another one. The generator picks
    /// the characters without bias, which a remainder of a random number over
    /// the size of the alphabet would not.
    /// </remarks>
    internal static string Draw() => new(RandomNumberGenerator.GetItems<char>(Alphabet, DrawnLength));

    /// <summary>Refuses text that is not an identifier of the model.</summary>
    /// <param name="value">The value the caller handed over.</param>
    /// <param name="parameterName">
    /// The parameter the value came from, so the message points at the caller's
    /// argument and not at a parameter of this class.
    /// </param>
    /// <returns>The value, so a constructor can assign the result of this check.</returns>
    /// <exception cref="ArgumentNullException">No value was handed over.</exception>
    /// <exception cref="ArgumentException">
    /// The value is empty or carries a character that
    /// <c>docs/Datenmodell.md</c> does not allow in an identifier.
    /// </exception>
    /// <remarks>
    /// The check is stricter than the draw on purpose: it accepts the underscore
    /// because <c>docs/Datenmodell.md</c> allows it and identifiers from an
    /// earlier hand or from a later draw may carry it, while the draw itself
    /// keeps to letters and digits. What matters is that nothing outside the
    /// permitted alphabet can ever reach a path.
    /// </remarks>
    internal static string Validate(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Length == 0)
        {
            throw new ArgumentException("An identifier names something and is therefore not empty.", parameterName);
        }

        foreach (var character in value)
        {
            var permitted = character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_';

            if (!permitted)
            {
                throw new ArgumentException(
                    "An identifier carries only lower case letters, digits and the underscore; see docs/Datenmodell.md, section Ablage.",
                    parameterName);
            }
        }

        return value;
    }
}
