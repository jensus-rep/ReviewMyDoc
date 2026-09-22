// The one setting the owner sign-in needs: the hash of the password. It is
// configuration and never a constant, because the person who receives this
// repository has to be able to set their own password without changing a line
// of code. Described for the operator in docs/Betrieb.md.

namespace ReviewMyDoc.Web.Security;

/// <summary>Everything under the configuration section <c>Owner</c>.</summary>
/// <remarks>
/// In Azure the same key is an App Setting with <c>__</c> as the separator:
/// <c>Owner__PasswordHash</c>.
/// </remarks>
public sealed class OwnerOptions
{
    /// <summary>The name of the section these options are bound from.</summary>
    public const string SectionName = "Owner";

    /// <summary>
    /// The hash of the password of the owner, as
    /// <c>src/ReviewMyDoc.Web/Security/PasswordHashCommand.cs</c> prints it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hash and never the password. Whoever reads the configuration of the
    /// running application, an App Setting in the portal or a backup of it,
    /// learns nothing they could sign in with, and a hash that costs work to
    /// check is worth little to whoever takes it away.
    /// </para>
    /// <para>
    /// Empty means: nobody can sign in. That is the deliberate behaviour of an
    /// unconfigured application and not a gap, and outside development an empty
    /// value stops the start instead, see
    /// <see cref="OwnerAuthenticationServiceCollectionExtensions"/>.
    /// </para>
    /// </remarks>
    public string PasswordHash { get; set; } = string.Empty;
}
