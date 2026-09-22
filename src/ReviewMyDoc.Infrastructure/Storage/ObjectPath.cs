// The one place that decides what a path of docs/Datenmodell.md may look like.
// It exists because both implementations of IObjectStore have to refuse exactly
// the same paths: if one of them accepted what the other rejects, a document
// would be storable on a developer machine and not in Azure, and no contract
// test would notice, because each implementation would be measured against its
// own idea of a valid path.

namespace ReviewMyDoc.Infrastructure.Storage;

/// <summary>
/// Checks paths and prefixes of the object store before they reach any storage.
/// </summary>
/// <remarks>
/// <para>
/// The rules come from <c>docs/Datenmodell.md</c> and from the remarks of
/// <see cref="ReviewMyDoc.Core.Storage.IObjectStore"/>: segments separated by a
/// forward slash, no leading slash, lower case throughout, no empty segment, no
/// part that begins with a dot and none of the characters a file name cannot
/// carry. Some of these rules only bite on a file system - <c>..</c> is an
/// ordinary blob name in Azure - and that is precisely why they are checked for
/// both stores. The point is not to defend against an attacker, because paths
/// are built by the application and never by a visitor, but to keep the two
/// stores from drifting apart in silence.
/// </para>
/// <para>
/// The checks throw <see cref="ArgumentException"/> and never touch the
/// storage, so every operation of both stores can run them first and a rejected
/// path costs no request.
/// </para>
/// </remarks>
internal static class ObjectPath
{
    /// <summary>
    /// The characters a Windows file name cannot carry. A path is refused
    /// because of them instead of failing later with an error of the file
    /// system that says nothing about the path. The colon is in the list twice
    /// over: it opens an alternate data stream and it begins a drive letter.
    /// </summary>
    private static readonly char[] ForbiddenCharacters = ['\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// The character no part of a path may begin with. It rules out <c>.</c>
    /// and <c>..</c>, which are the way out of a root directory, and it keeps
    /// the names the local store uses for its own bookkeeping free.
    /// </summary>
    internal const string DotPrefix = ".";

    /// <summary>Refuses everything that is not a path of the model.</summary>
    /// <param name="path">The value the caller handed over.</param>
    /// <param name="parameterName">
    /// The parameter the value came from, so the message points at the caller's
    /// argument and not at a parameter of this class.
    /// </param>
    /// <exception cref="ArgumentNullException">No value was handed over.</exception>
    /// <exception cref="ArgumentException">The value is not a path of the model.</exception>
    internal static void Validate(string path, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(path, parameterName);

        if (path.Length == 0)
        {
            throw new ArgumentException("A path names an entry and is therefore not empty.", parameterName);
        }

        ValidateCharacters(path, parameterName);
        ValidateIsLowerCase(path, parameterName);

        foreach (var segment in path.Split('/'))
        {
            ValidateSegment(segment, path, parameterName, mayBeEmpty: false);
        }
    }

    /// <summary>Refuses a prefix that no path of the model could begin with.</summary>
    /// <param name="prefix">The value the caller handed over; the empty prefix matches everything and is allowed.</param>
    /// <param name="parameterName">The parameter the value came from.</param>
    /// <exception cref="ArgumentNullException">No value was handed over.</exception>
    /// <exception cref="ArgumentException">No path of the model could begin with the value.</exception>
    internal static void ValidatePrefix(string prefix, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(prefix, parameterName);

        ValidateCharacters(prefix, parameterName);
        ValidateIsLowerCase(prefix, parameterName);

        // A prefix may end in the middle of a name and it may end with a
        // separator, so only its last part may be empty.
        var segments = prefix.Split('/');
        for (var index = 0; index < segments.Length; index++)
        {
            ValidateSegment(segments[index], prefix, parameterName, mayBeEmpty: index == segments.Length - 1);
        }
    }

    /// <summary>Refuses the characters a path of the model never carries.</summary>
    private static void ValidateCharacters(string value, string parameterName)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character) || Array.IndexOf(ForbiddenCharacters, character) >= 0)
            {
                throw new ArgumentException(
                    "A path uses the forward slash as its only separator and carries none of the characters a file name cannot hold.",
                    parameterName);
            }
        }
    }

    /// <summary>Refuses an upper case letter anywhere in a path.</summary>
    /// <remarks>
    /// Blob names tell case apart and a Windows file system does not, so a path
    /// that relied on case would mean two entries in one store and one in the
    /// other. <c>docs/Datenmodell.md</c> answers that by drawing every
    /// identifier from a lower case alphabet and having the store refuse
    /// anything else. The rule covers the whole path, directories and the name
    /// of the entry alike, because the entry names are drawn the same way the
    /// directory names are.
    /// </remarks>
    private static void ValidateIsLowerCase(string value, string parameterName)
    {
        foreach (var character in value)
        {
            if (char.IsUpper(character))
            {
                throw new ArgumentException(
                    "A path is lower case; identifiers are drawn from a lower case alphabet.",
                    parameterName);
            }
        }
    }

    /// <summary>Refuses one part of a path that does not name anything.</summary>
    private static void ValidateSegment(string segment, string value, string parameterName, bool mayBeEmpty)
    {
        if (segment.Length == 0)
        {
            if (mayBeEmpty)
            {
                return;
            }

            // Catches the leading slash of an absolute path as well as a
            // doubled separator.
            throw new ArgumentException(
                $"The path '{value}' has an empty part; it is relative to the container and has no empty names.",
                parameterName);
        }

        // A part that begins with a dot is ".", ".." or a name of the store's
        // own bookkeeping. None of them names an entry, and the first two are
        // the way out of the root.
        if (segment.StartsWith(DotPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The path '{value}' has a part that begins with a dot; no entry of the model does.",
                parameterName);
        }

        // Windows drops a trailing dot or space from a name without a word,
        // which would store the entry under a name nobody asked for.
        if (segment[^1] is '.' or ' ')
        {
            throw new ArgumentException(
                $"The path '{value}' has a part that ends with a dot or a space.",
                parameterName);
        }
    }
}
