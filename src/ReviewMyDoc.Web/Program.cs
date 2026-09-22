// Composition root of the web application: services, middleware and routing all
// come together here, so there is exactly one place that answers what happens to
// every request. See docs/Konventionen.md, section Struktur.

using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using ReviewMyDoc.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Which of the two object stores this is, Azure or a local directory, the
// section "Storage" of the configuration decides; see docs/Betrieb.md. The
// choice is made in the infrastructure assembly so that no Azure type has to be
// named here.
builder.Services.AddObjectStore(builder.Configuration);

var app = builder.Build();

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
