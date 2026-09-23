// Composition root of the web application: services, middleware and routing all
// come together here, so there is exactly one place that answers what happens to
// every request. See docs/Konventionen.md, section Struktur.

using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.WebEncoders;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Reviews;
using ReviewMyDoc.Infrastructure.Markdown;
using ReviewMyDoc.Infrastructure.Security;
using ReviewMyDoc.Infrastructure.Storage;
using ReviewMyDoc.Web.Security;

// Before anything else: the application can be called to print a password hash
// instead of serving. It stands at the top because that command needs no
// service, no store and no key ring, and starting them for it would be wrong in
// the one case that matters, an operator running it on a machine that has no
// configuration yet. See src/ReviewMyDoc.Web/Security/PasswordHashCommand.cs.
if (PasswordHashCommand.TryRun(args, out var commandExitCode))
{
    return commandExitCode;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Umlauts stay umlauts in the markup. By default the HTML encoder escapes every
// character outside Basic Latin as a numeric entity, so a title a person typed
// arrives in the page as "Musterstra&#xDF;e" - correct in a browser, unreadable
// in the source, and impossible to search for in a test. The interface of this
// application is German, so the whole range is allowed through. This changes
// nothing about safety: the characters that carry meaning in HTML, < > & " and
// the apostrophe, are still escaped, and every page is delivered as UTF-8.
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Which of the two object stores this is, Azure or a local directory, the
// section "Storage" of the configuration decides; see docs/Betrieb.md. The
// choice is made in the infrastructure assembly so that no Azure type has to be
// named here.
builder.Services.AddObjectStore(builder.Configuration);

// The document aggregate of ReviewMyDoc.Core: a store that turns it into the
// entries of docs/Datenmodell.md through the object store above, and the
// service the pages under Pages/Dokumente/ call. Both are stateless singletons
// over the one object store, and the clock is the real one everywhere but in a
// test, which hands the service a fixed one instead.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDocumentStore, DocumentStore>();
builder.Services.AddSingleton<DocumentService>();
builder.Services.AddSingleton<DocumentEditingService>();
builder.Services.AddSingleton<ReviewStore>();
builder.Services.AddSingleton<ReviewService>();
builder.Services.AddSingleton<ReviewPipeline>();

// Markdown to safe HTML, the one translation a section's text takes on its way
// to a page; the renderer lives in the infrastructure assembly because Markdig
// does, see Markdown/MarkdigMarkdownRenderer.cs.
builder.Services.AddMarkdownRenderer();

// The keys that encrypt every cookie and every antiforgery token. They go to the
// same place the documents go, decided by the same setting, and they outlive a
// restart of the application; without that, every sign-in and every open form
// would end whenever the application is restarted, moved or scaled. Registered
// in the infrastructure assembly for the same reason as the store above: no
// Azure type is named here.
builder.Services.AddDataProtectionKeys(builder.Configuration);

// The named rate limiters of sign-in, review link and AI endpoints. A page asks
// its limiter itself and answers a refusal with 429 inside its own frame, which
// is why these are services and not policies of the RateLimiter middleware; see
// src/ReviewMyDoc.Web/Security/RateLimitServiceCollectionExtensions.cs.
builder.Services.AddRateLimits(builder.Configuration);

// The sign-in of the owner: the cookie, the check of the password against the
// configured hash and the rule that every endpoint demands the role owner
// unless it says otherwise. The rule is a fallback policy and not a list, so a
// page added later is protected without anybody remembering to protect it; see
// src/ReviewMyDoc.Web/Security/OwnerAuthenticationServiceCollectionExtensions.cs.
builder.Services.AddOwnerAuthentication(builder.Configuration, builder.Environment);

var app = builder.Build();

// First in the pipeline, so that a rendered page, a static file, a redirect, a
// 404 and an error page all carry the same headers. See
// src/ReviewMyDoc.Web/Security/SecurityHeaders.cs, which explains every single
// one of them.
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

// The building blocks are delivered from the repository folder components/
// under /components/, with a file provider of their own. That way tokens and
// building blocks exist exactly once, nothing of them is copied into wwwroot
// and a change to a building block is one change, not two. See
// docs/Konventionen.md, section Struktur, and components/README.md.
//
// Only the extensions the browser really asks for are served. Everything else,
// above all the demo.html and the README.md of a building block, has no type
// here and therefore no answer: those pages belong to the repository, not to
// the running application. A path with .. reaches nothing above the folder
// either, because the file provider rejects it.
var componentTypes = new FileExtensionContentTypeProvider(
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".css"] = "text/css",
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".woff2"] = "font/woff2",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
    });

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(FindComponentsFolder(app.Environment.ContentRootPath)),
    RequestPath = "/components",
    ContentTypeProvider = componentTypes,
    ServeUnknownFileTypes = false,
});

// The two stylesheet folders above are served before this line and are
// therefore the only thing of this application an anonymous request ever gets.
// That is deliberate: the sign-in page has to be styled, and a stylesheet is
// the same for everybody. Everything that is routed lies behind the
// authorization below.
app.UseRouting();

// A request that matched no endpoint ends here, with 404 and nothing else.
//
// Without this, the fallback policy below would take it as well, because
// authorization applies its fallback to a request without an endpoint too, and
// an address that does not exist would answer with a redirect to the sign-in
// form. That would be wrong twice: the delivery of /components/ promises that a
// path with .. and a file that is not served end in 404, and a 404 that is
// dressed up as a sign-in tells the visitor that the address might exist. It
// hides nothing either, because which pages this application has is written in
// its repository, which is public.
app.Use(async (context, next) =>
{
    if (context.GetEndpoint() is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;

        return;
    }

    await next(context);
});

// Authentication before authorization: the cookie has to become a user before
// the fallback policy can be checked against that user.
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

// The exit code of the server, told apart from the codes of the command above:
// a server that was stopped ended well.
return 0;

// Finds the folder components/, walking up from the content root. Published, it
// lies next to the application, during development it stays in the repository
// above the web project; walking up finds both without a setting that could be
// wrong in one of the two cases. Missing, it stops the application at startup:
// a running application without a single style would be the worse answer.
static string FindComponentsFolder(string contentRootPath)
{
    for (var directory = new DirectoryInfo(contentRootPath); directory is not null; directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "components");

        if (Directory.Exists(candidate))
        {
            return candidate;
        }
    }

    throw new InvalidOperationException(
        $"The folder components/ was not found above the content root '{contentRootPath}'. " +
        "It is delivered under /components/ and is part of the published output.");
}

/// <summary>
/// Makes the implicitly generated entry point visible to the test project, so
/// integration tests can name it as the type argument of WebApplicationFactory.
/// </summary>
public partial class Program;
