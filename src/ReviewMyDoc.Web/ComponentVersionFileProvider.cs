// Exposes the component folder at its public URL to the Razor file-version
// helper. Static file delivery uses a separate, restricted provider in Program.
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace ReviewMyDoc.Web;

internal sealed class ComponentVersionFileProvider(string componentsFolder) : IFileProvider
{
    private readonly PhysicalFileProvider _files = new(componentsFolder);

    public IFileInfo GetFileInfo(string subpath) => Map(subpath) is { } path
        ? _files.GetFileInfo(path)
        : new NotFoundFileInfo(subpath);

    public IDirectoryContents GetDirectoryContents(string subpath) => Map(subpath) is { } path
        ? _files.GetDirectoryContents(path)
        : NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => Map(filter) is { } path
        ? _files.Watch(path)
        : NullChangeToken.Singleton;

    private static string? Map(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Equals("components", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        const string prefix = "components/";
        return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[prefix.Length..]
            : null;
    }
}
