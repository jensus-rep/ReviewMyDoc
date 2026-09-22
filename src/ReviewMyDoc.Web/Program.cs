// Composition root of the web application: services, middleware and routing all
// come together here, so there is exactly one place that answers what happens to
// every request. See docs/Konventionen.md, section Struktur.

using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using ReviewMyDoc.Infrastructure.Security;
using ReviewMyDoc.Infrastructure.Storage;
using ReviewMyDoc.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    // The check pages exist for development and for the integration tests. In a
    // deployed application they have no address; see ProbePagesConvention.
    if (!builder.Environment.IsDevelopment())
    {
        options.Conventions.Add(new ProbePagesConvention());
    }
});

// Which of the two object stores this is, Azure or a local directory, the
// section "Storage" of the configuration decides; see docs/Betrieb.md. The
// choice is made in the infrastructure assembly so that no Azure type has to be
// named here.
builder.Services.AddObjectStore(builder.Configuration);

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

app.UseRouting();
app.MapRazorPages();

app.Run();

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
