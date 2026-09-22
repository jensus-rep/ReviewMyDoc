// Guards the rule that tokens and building blocks exist exactly once, at the
// place where breaking it is cheapest: a copy under wwwroot. A copy looks right
// on the day it is made and drifts away from the original from the next change
// on, and nothing in a running application would ever say so.

using System.Xml.Linq;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Checks where the building blocks lie and where they do not.</summary>
public sealed class ComponentsFolderTests
{
    [Fact]
    public void The_building_blocks_lie_in_the_repository_folder()
    {
        var components = Path.Combine(RepositoryRoot(), "components");

        Assert.True(File.Exists(Path.Combine(components, "tokens", "tokens.css")));
        Assert.True(File.Exists(Path.Combine(components, "button", "button.css")));
        Assert.True(File.Exists(Path.Combine(components, "field", "field.css")));
        Assert.True(File.Exists(Path.Combine(components, "rows", "rows.css")));
    }

    // No copy under wwwroot, in no name and in no folder: the application reads
    // the building blocks from components/ and from nowhere else.
    [Fact]
    public void No_building_block_was_copied_into_wwwroot()
    {
        var webRoot = Path.Combine(RepositoryRoot(), "src", "ReviewMyDoc.Web", "wwwroot");

        var names = Directory.EnumerateFiles(webRoot, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToArray();

        Assert.DoesNotContain("tokens.css", names);
        Assert.DoesNotContain("button.css", names);
        Assert.DoesNotContain("field.css", names);
        Assert.DoesNotContain("rows.css", names);
        Assert.False(Directory.Exists(Path.Combine(webRoot, "components")));
    }

    // Published, the building blocks have to lie next to the application, or
    // /components/ answers with nothing at all on the server.
    [Fact]
    public void The_web_project_copies_the_building_blocks_into_the_published_output()
    {
        var project = XDocument.Load(
            Path.Combine(RepositoryRoot(), "src", "ReviewMyDoc.Web", "ReviewMyDoc.Web.csproj"));

        var copied = project.Descendants("Content")
            .Where(content => (content.Attribute("Include")?.Value ?? string.Empty).Contains("components"))
            .ToArray();

        var content = Assert.Single(copied);
        Assert.Equal("PreserveNewest", content.Attribute("CopyToPublishDirectory")?.Value);
        Assert.StartsWith("components", content.Attribute("Link")?.Value, StringComparison.Ordinal);
    }

    // site.css says what is not a building block, and it says it in the values
    // of the tokens. A raw colour or a raw distance here would be the one place
    // that does not follow light and dark mode.
    [Fact]
    public void The_styles_of_the_application_use_no_raw_values()
    {
        var css = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "src", "ReviewMyDoc.Web", "wwwroot", "css", "site.css"));

        Assert.DoesNotContain("#", css, StringComparison.Ordinal);
        Assert.DoesNotContain("rgb(", css, StringComparison.Ordinal);
        Assert.DoesNotContain("px", css, StringComparison.Ordinal);
        Assert.DoesNotContain("rem", css, StringComparison.Ordinal);
    }

    /// <summary>Walks up from the test output until the solution file appears.</summary>
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
