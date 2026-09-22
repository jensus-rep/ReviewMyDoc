// Puts the owner sign-in into the service container: the cookie that carries a
// session, the check of the password and, above all, the rule that a page is
// protected unless it says otherwise. The last one is the reason this file
// exists at all. A list of protected pages would be right on the day it is
// written and wrong on the day somebody adds the next page, so there is no
// list.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace ReviewMyDoc.Web.Security;

/// <summary>Registers authentication and authorization of the owner.</summary>
public static class OwnerAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Binds the section <c>Owner</c>, registers the cookie authentication and
    /// makes the role <see cref="OwnerAuthentication.Role"/> the default demand
    /// of every endpoint.
    /// </summary>
    /// <param name="services">The container of the application.</param>
    /// <param name="configuration">The configuration the section is read from.</param>
    /// <param name="environment">
    /// The environment. It decides two things: whether a cookie may travel over
    /// plain http, and whether the application is allowed to start without a
    /// password hash.
    /// </param>
    /// <returns>The container, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Outside development and no password hash is configured.
    /// </exception>
    public static IServiceCollection AddOwnerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var section = configuration.GetSection(OwnerOptions.SectionName);

        services.Configure<OwnerOptions>(section);

        // A deployed application without a hash could not be signed into by
        // anybody, and it would say so only to whoever tried. It is stopped
        // here, at the one moment somebody is watching, and the message names
        // the key. This is the same choice the key ring makes for a missing
        // Azure connection; see docs/Betrieb.md.
        if (!environment.IsDevelopment()
            && string.IsNullOrWhiteSpace(section[nameof(OwnerOptions.PasswordHash)]))
        {
            throw new InvalidOperationException(
                $"The setting {OwnerOptions.SectionName}:{nameof(OwnerOptions.PasswordHash)} "
                + $"(App Setting {OwnerOptions.SectionName}__{nameof(OwnerOptions.PasswordHash)}) is missing. "
                + "Without it nobody could sign in. docs/Betrieb.md says how the hash is created and set.");
        }

        // A singleton: it holds no request state, and the hash it falls back to
        // when nothing is configured is worth building once instead of per
        // attempt.
        services.AddSingleton<OwnerPasswordCheck>();

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.Cookie.Name = OwnerAuthentication.CookieName;

                // No script of this application reads the cookie, so no script
                // may.
                cookie.Cookie.HttpOnly = true;

                // Lax and not Strict: the redirect that follows the sign-in and
                // every link that leads into the application from outside, a
                // bookmark or a mail, have to carry the cookie. Strict would
                // drop it there and show the sign-in form to somebody who is
                // signed in.
                cookie.Cookie.SameSite = SameSiteMode.Lax;

                // Never over plain http outside development. In development the
                // application runs on http://localhost:5071, where Always would
                // mean no cookie at all and therefore no sign-in; see
                // docs/Konventionen.md, section Tests.
                cookie.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;

                cookie.ExpireTimeSpan = OwnerAuthentication.SessionLifetime;
                cookie.SlidingExpiration = false;

                cookie.LoginPath = OwnerAuthentication.LoginPath;
                cookie.LogoutPath = OwnerAuthentication.LogoutPath;
                cookie.ReturnUrlParameter = "ReturnUrl";

                // Somebody who carries a valid cookie of another kind, the
                // review link session of Epic 2, is not refused with a bare 403
                // but shown the form of the owner. There is nothing else this
                // application could offer them.
                cookie.AccessDeniedPath = OwnerAuthentication.LoginPath;
            });

        // The rule of the whole application, in one place: an endpoint that says
        // nothing about authorization demands the role owner. It is a fallback
        // policy and not a list of folders, because a fallback needs nobody to
        // remember it. Whoever adds a page finds it protected; whoever wants an
        // open page has to write [AllowAnonymous] on it, which is a sentence in
        // a review and a line in a diff, and AuthorizationDefaultTests holds the
        // one list of pages that are allowed to carry it.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole(OwnerAuthentication.Role)
                .Build());

        return services;
    }
}
