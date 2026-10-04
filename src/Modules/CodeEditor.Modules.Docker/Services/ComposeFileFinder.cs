using System.Text.RegularExpressions;
using CodeEditor.Core.Files;
using CodeEditor.Core.Text;

namespace CodeEditor.Modules.Docker.Services;

/// <summary>
/// Workspace compose files from the file index: <c>compose.yaml</c>, <c>docker-compose.yml</c> and their variants
/// (<c>docker-compose.override.yml</c>, <c>compose.dev.yaml</c>). The index has already dropped excluded folders
/// (<c>node_modules</c>, <c>.gitignore</c> rules). Root files first, then by depth and name.
/// </summary>
public static partial class ComposeFileFinder
{
    /// <summary>File limit for the panel: docker reads each project in a separate process.</summary>
    public const int MaxFiles = 20;

    /// <remarks>One pass over the index plus sorting the matches.</remarks>
    public static IReadOnlyList<string> Find(IReadOnlyList<IndexedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return
        [
            .. files.Where(file => ComposeName().IsMatch(file.Name))
                .Select(file => file.RelativePath)
                .OrderBy(path => path.Count(character => character == '/'))
                .ThenBy(path => path, NaturalStringComparer.Instance)
                .Take(MaxFiles),
        ];
    }

    [GeneratedRegex(@"^(?:docker-)?compose(?:[.-][\w.-]+)?\.ya?ml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ComposeName();
}
