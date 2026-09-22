// Composition root of the web application: services, middleware and routing all
// come together here, so there is exactly one place that answers what happens to
// every request. See docs/Konventionen.md, section Struktur.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

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
