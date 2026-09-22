// The security headers of every response, in one middleware. They stand here
// and never in a page, because a header that a page has to remember is a header
// that the next page forgets. Whoever adds a page later gets these answers
// without doing anything, and whoever wants to change one of them changes it
// here, once. See docs/Konventionen.md, section Code, and docs/Betrieb.md.

namespace ReviewMyDoc.Web.Security;

/// <summary>
/// The headers this application puts on every response, and the middleware that
/// does it.
/// </summary>
/// <remarks>
/// Each value below is chosen for this application and not copied from a list.
/// What makes the policy tight is what ReviewMyDoc is: server rendered Razor
/// Pages, styles from <c>/components/</c> and <c>/css/</c> of its own origin, no
/// framework, no CDN, no third party at all. Nothing here is loosened for a
/// case that does not exist.
/// </remarks>
public static class SecurityHeaders
{
    /// <summary>
    /// Content Security Policy: what the browser may fetch and execute.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>default-src 'none'</c> is the base, not <c>'self'</c>. It means every
    /// kind of fetch has to be named below, so a resource kind nobody thought
    /// about, a web worker or a manifest for instance, is refused instead of
    /// silently inheriting a permission. That also covers <c>object-src</c>,
    /// <c>frame-src</c>, <c>media-src</c> and <c>worker-src</c>, which is why
    /// none of them is written out.
    /// </para>
    /// <para>
    /// <c>script-src 'self'</c> and, above all, <c>style-src 'self'</c> without
    /// <c>'unsafe-inline'</c>. This is the one place where ReviewMyDoc is
    /// stricter than Atelier, and it is deliberate: the frame and the partials
    /// under <c>Pages/Shared/Ui/</c> carry classes only and not a single
    /// <c>style</c> attribute, and every building block brings its own file
    /// under <c>/components/</c>. Without <c>'unsafe-inline'</c> an injected
    /// <c>style</c> attribute cannot repaint a page into a login form and an
    /// injected <c>&lt;script&gt;</c> cannot run at all, which is most of what a
    /// Content Security Policy is for. The price is the developer exception page
    /// of ASP.NET Core, which brings inline styles and therefore appears
    /// unstyled in development; its text stays readable, and a policy that is
    /// weakened for an error page nobody outside development ever sees would be
    /// the worse trade. Whoever adds a page must keep to files, and the test
    /// <c>SecurityHeaderTests</c> checks the rendered frame for inline styles
    /// and scripts, so the rule is not only written down.
    /// </para>
    /// <para>
    /// <c>font-src 'self'</c> is needed: <c>components/tokens/website.css</c>
    /// loads three woff2 files next to it. <c>img-src 'self'</c> without
    /// <c>data:</c>, because no stylesheet and no page uses a data URI today;
    /// whoever needs one changes this line and says why.
    /// <c>connect-src 'self'</c> is what the coming AI endpoints talk to over
    /// server sent events, and nothing else.
    /// </para>
    /// <para>
    /// <c>form-action 'self'</c> keeps a form of this application from posting a
    /// document, a review or a password anywhere else. <c>base-uri 'none'</c>,
    /// not <c>'self'</c>: this application never writes a <c>&lt;base&gt;</c>
    /// element, and an injected one would silently re-point every relative
    /// address on the page. <c>frame-ancestors 'none'</c> forbids framing, which
    /// is the answer to clickjacking on the button that hands a document out for
    /// review.
    /// </para>
    /// </remarks>
    public const string ContentSecurityPolicy =
        "default-src 'none'; "
        + "script-src 'self'; "
        + "style-src 'self'; "
        + "img-src 'self'; "
        + "font-src 'self'; "
        + "connect-src 'self'; "
        + "form-action 'self'; "
        + "base-uri 'none'; "
        + "frame-ancestors 'none'";

    /// <summary>
    /// Permissions Policy: the device capabilities the browser may hand to this
    /// application.
    /// </summary>
    /// <remarks>
    /// All of them off. ReviewMyDoc writes, reads and comments on text; it has
    /// no reason to ask for a camera, a microphone, a location or a payment, now
    /// or later, and a capability that is switched off cannot be asked for by
    /// injected code either. The list names the features browsers actually
    /// honour; an unknown name in this header is ignored, so naming one costs
    /// nothing and missing one costs the protection.
    /// </remarks>
    public const string PermissionsPolicy =
        "accelerometer=(), ambient-light-sensor=(), autoplay=(), battery=(), bluetooth=(), camera=(), "
        + "display-capture=(), document-domain=(), encrypted-media=(), fullscreen=(), gamepad=(), "
        + "geolocation=(), gyroscope=(), hid=(), idle-detection=(), local-fonts=(), magnetometer=(), "
        + "microphone=(), midi=(), payment=(), picture-in-picture=(), publickey-credentials-get=(), "
        + "screen-wake-lock=(), serial=(), usb=(), xr-spatial-tracking=()";

    /// <summary>
    /// Puts the headers on every response of the pipeline that follows.
    /// </summary>
    /// <param name="app">The pipeline of the application.</param>
    /// <returns>The pipeline, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// Registered as the very first middleware, so that a redirect, a static
    /// file, a 404 and a rendered page carry the same headers. A header that
    /// only a rendered page gets is a header that is missing exactly where
    /// something already went wrong.
    /// </para>
    /// <para>
    /// The headers are written in a callback on
    /// <see cref="HttpResponse.OnStarting(Func{object, Task}, object)"/> and not
    /// on the way in. That is not a detail: the exception handler clears the
    /// response before it renders <c>/Error</c>, and it re-enters the pipeline
    /// behind this middleware, so headers written on the way in would be gone
    /// from exactly the response of a failed request. A callback runs at the
    /// moment the response really starts, after any clearing, and therefore
    /// cannot be lost.
    /// </para>
    /// </remarks>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(static async (context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                Apply((HttpResponse)state);

                return Task.CompletedTask;
            }, context.Response);

            await next(context);
        });
    }

    /// <summary>Writes the headers into one response.</summary>
    /// <param name="response">The response that is about to start.</param>
    /// <remarks>
    /// Assignment and not "add if absent": this is the one place that decides,
    /// and a second opinion written somewhere else must not survive it.
    /// </remarks>
    private static void Apply(HttpResponse response)
    {
        var headers = response.Headers;

        // A file is what its Content-Type says it is. Without this, a browser
        // may sniff an uploaded document into something it executes, and
        // uploaded documents are what this application is about.
        headers["X-Content-Type-Options"] = "nosniff";

        // A review link carries its token in the path. Sending the full address
        // to a foreign site as a referrer would hand that token out, so only the
        // bare origin leaves this application, and only towards HTTPS.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Says the same as frame-ancestors 'none' above, for the sake of the
        // intermediaries and embedded web views that still read only this older
        // header. Two headers can contradict each other; these two cannot,
        // because both forbid framing outright.
        headers["X-Frame-Options"] = "DENY";

        // A window this application opens, and a window that opened it, get no
        // handle to its browsing context. That also puts the page in its own
        // process in current browsers, so a side channel in a foreign tab has
        // nothing of this one to read.
        headers["Cross-Origin-Opener-Policy"] = "same-origin";

        // Nothing of this application, not a page, not a building block under
        // /components/, may be embedded as a resource by a foreign site.
        // /components/ is the repository folder of this application, not a CDN.
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        // The whole application is private: the owner signs in, a reviewer
        // arrives over a link that must never be in an index. There is no public
        // page to lose, so this is set for every response rather than for the
        // review paths alone, which would be one forgotten path away from a
        // leaked token.
        headers["X-Robots-Tag"] = "noindex, nofollow";

        headers["Permissions-Policy"] = PermissionsPolicy;
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
    }
}
