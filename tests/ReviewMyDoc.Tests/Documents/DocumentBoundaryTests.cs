// Guards the one boundary of the domain that a project file cannot guard.
// ProjectBoundaryTests keeps ASP.NET and Azure out of ReviewMyDoc.Core through
// its package references; System.IO needs no reference at all and would slip in
// unnoticed, and with it the assumption that a document lies in a file.

namespace ReviewMyDoc.Tests.Documents;

/// <summary>Checks what the document aggregate is allowed to name.</summary>
/// <remarks>
/// The domain reaches storage through <c>IObjectStore</c> alone. A type from
/// <c>System.IO</c> here would mean a path, a stream or a directory in the
/// domain, and the Azure implementation would then be carrying a file system
/// around. The check reads the sources, because that is where the difference is
/// visible at all.
/// </remarks>
public sealed class DocumentBoundaryTests
{
    private static readonly string[] Forbidden =
    [
        "System.IO",
        "Microsoft.AspNetCore",
        "Azure.",
    ];

    [Fact]
    public void The_document_aggregate_names_nothing_of_asp_net_of_azure_or_of_the_file_system()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "ReviewMyDoc.Core", "Documents");
        var sources = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);

        Assert.NotEmpty(sources);
        foreach (var source in sources)
        {
            var content = File.ReadAllText(source);

            foreach (var forbidden in Forbidden)
            {
                Assert.DoesNotContain(forbidden, content, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Walks up from the test output directory until the solution file appears,
    /// so the test does not depend on where the runner puts the binaries.
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
