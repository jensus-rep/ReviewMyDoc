// The names and the one identity of the owner sign-in. ReviewMyDoc has exactly
// one person who may see the application, so the whole account model is a role
// and a cookie; this file holds the few names both sides of that agree on, so
// neither a page nor a test writes "owner" as a string of its own.

using System.Security.Claims;

namespace ReviewMyDoc.Web.Security;

/// <summary>
/// The marker type of the one account. It carries nothing: there is no user
/// store, no profile and no second account, and
/// <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/> never reads
/// the user it is typed on. It exists so that hashing and checking name the same
/// type and cannot drift apart.
/// </summary>
public sealed class OwnerAccount
{
    /// <summary>The one instance; there is nothing to distinguish two of them.</summary>
    public static readonly OwnerAccount Instance = new();

    private OwnerAccount()
    {
    }
}

/// <summary>Names and identity of the owner sign-in.</summary>
public static class OwnerAuthentication
{
    /// <summary>
    /// The role every page of the application demands. It is a role and not
    /// merely "signed in", because the reviewer link session that comes with
    /// Epic 2 will also be a signed cookie: a page that only asked for an
    /// authenticated user would let a reviewer in.
    /// </summary>
    public const string Role = "owner";

    /// <summary>The address of the sign-in page.</summary>
    public const string LoginPath = "/anmeldung";

    /// <summary>The address the sign-out form posts to.</summary>
    public const string LogoutPath = "/abmelden";

    /// <summary>
    /// The name of the cookie. Named after the application and not left at the
    /// default, so it is recognisable in a browser and cannot collide with
    /// another application on the same host name.
    /// </summary>
    public const string CookieName = "ReviewMyDoc.Owner";

    /// <summary>
    /// How long a signed-in session lasts before it has to be renewed by signing
    /// in again.
    /// </summary>
    /// <remarks>
    /// Eight hours and not sliding: the session ends with the working day
    /// instead of extending itself for as long as a tab stays open. Taken from
    /// Atelier, where the same span is used and argued.
    /// </remarks>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    /// <summary>Builds the identity that is written into the cookie.</summary>
    /// <returns>The principal of the owner, carrying the role and nothing else.</returns>
    /// <remarks>
    /// Name and role are the same word. There is one account, so a name that
    /// said anything more would be a personal datum in a cookie without being of
    /// any use to the application; see docs/Konventionen.md, section Code.
    /// </remarks>
    public static ClaimsPrincipal CreatePrincipal()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, Role), new Claim(ClaimTypes.Role, Role)],
            authenticationType: CookieName,
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
