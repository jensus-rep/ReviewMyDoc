// Guards the project boundaries from docs/Konventionen.md at the place where
// they are declared: the project files. A domain project that quietly gains a
// reference to ASP.NET or Azure is the one mistake that is cheap to make and
// expensive to undo, so it fails the build instead of a review.

using System.Xml.Linq;

namespace ReviewMyDoc.Tests;

/// <summary>Checks what every project file may and may not reference.</summary>
public sealed class ProjectBoundaryTests
{
    private static readonly string[] ForbiddenInCore =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Azure.",
        "Anthropic",
        "OpenAI",
    ];

    [Fact]
    public void Core_references_no_infrastructure_package()
    {
        var project = LoadProject("src/ReviewMyDoc.Core/ReviewMyDoc.Core.csproj");

        var packages = project.Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();

        foreach (var package in packages)
        {
            Assert.DoesNotContain(ForbiddenInCore, forbidden =>
                package.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Core_references_no_other_project()
    {
        var project = LoadProject("src/ReviewMyDoc.Core/ReviewMyDoc.Core.csproj");

        Assert.Empty(project.Descendants("ProjectReference"));
    }

    // The web project registers the object store and the Data Protection key
    // ring, and both of them may end up in Azure. It must still not know that:
    // the registration lives in Infrastructure precisely so that Program.cs
    // names no Azure type and the application could be moved without touching a
    // page. A package reference would be the first step back.
    [Fact]
    public void The_web_project_references_no_azure_package()
    {
        var project = LoadProject("src/ReviewMyDoc.Web/ReviewMyDoc.Web.csproj");

        var packages = project.Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value ?? string.Empty);

        Assert.DoesNotContain(packages, package =>
            package.StartsWith("Azure.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Infrastructure_does_not_reference_the_web_project()
    {
        var project = LoadProject("src/ReviewMyDoc.Infrastructure/ReviewMyDoc.Infrastructure.csproj");

        var references = project.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value ?? string.Empty);

        Assert.DoesNotContain(references, reference => reference.Contains("ReviewMyDoc.Web"));
    }

    [Theory]
    [InlineData("src/ReviewMyDoc.Core/ReviewMyDoc.Core.csproj")]
    [InlineData("src/ReviewMyDoc.Infrastructure/ReviewMyDoc.Infrastructure.csproj")]
    [InlineData("src/ReviewMyDoc.Web/ReviewMyDoc.Web.csproj")]
    [InlineData("tests/ReviewMyDoc.Tests/ReviewMyDoc.Tests.csproj")]
    public void Shared_settings_are_not_repeated_in_a_project_file(string relativePath)
    {
        var project = LoadProject(relativePath);

        var repeated = project.Descendants()
            .Select(element => element.Name.LocalName)
            .Where(name => name is "TargetFramework" or "Nullable" or "ImplicitUsings"
                or "LangVersion" or "TreatWarningsAsErrors")
            .ToArray();

        Assert.Empty(repeated);
    }

    private static XDocument LoadProject(string relativePath) =>
        XDocument.Load(Path.Combine(RepositoryRoot(), relativePath));

    /// <summary>
    /// Walks up from the test output directory until the solution file appears,
    /// so the tests do not depend on where the runner puts the binaries.
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ReviewMyDoc.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
