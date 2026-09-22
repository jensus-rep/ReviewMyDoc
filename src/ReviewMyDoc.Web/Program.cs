// Composition root of the web application: services, middleware and routing all
// come together here, so there is exactly one place that answers what happens to
// every request. See docs/Konventionen.md, section Struktur.

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
app.UseRouting();
app.MapRazorPages();

app.Run();

/// <summary>
/// Makes the implicitly generated entry point visible to the test project, so
/// integration tests can name it as the type argument of WebApplicationFactory.
/// </summary>
public partial class Program;
