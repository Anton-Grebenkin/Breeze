using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Terminal.Resources;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// What to build and test: the given project or solution, else the root solution (<c>.slnx</c>, <c>.sln</c>), else the
/// only project in the root. A folder resolves to its solution or only project, as with <c>dotnet test</c> (models
/// often pass <c>tests/Calc.Tests</c>). Paths are confined to the workspace.
/// </summary>
public sealed class DotNetTarget(IWorkspace workspace, IFileSystem fileSystem)
{
    private static readonly string[] SolutionExtensions = [".slnx", ".sln"];
    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];

    /// <exception cref="AgentToolException">No folder is open, the path is not buildable, or nothing to build.</exception>
    public string Resolve(string? project)
    {
        var root = workspace.Root ?? throw new AgentToolException(Strings.NoWorkspace);
        if (!string.IsNullOrWhiteSpace(project))
        {
            var full = WorkspacePaths.Resolve(workspace, project);
            if (fileSystem.DirectoryExists(full) && FindDefault(full) is { } inFolder)
            {
                return inFolder;
            }

            return fileSystem.FileExists(full) && IsBuildable(full)
                ? full
                : throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.NotBuildable, project));
        }

        return FindDefault(root)
            ?? throw new AgentToolException(Strings.NothingToBuild);
    }

    /// <summary>The root solution or only root project; <c>null</c> if there is no default target.</summary>
    public string? FindDefault() => workspace.Root is { } root ? FindDefault(root) : null;

    public static bool IsBuildable(string path) => HasExtension(path, SolutionExtensions) || HasExtension(path, ProjectExtensions);

    public static bool IsProject(string path) => HasExtension(path, ProjectExtensions);

    // Span comparison: called for every indexed file, so no extension string is allocated.
    private static bool HasExtension(string path, string[] extensions)
    {
        var extension = Path.GetExtension(path.AsSpan());
        foreach (var candidate in extensions)
        {
            if (extension.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string? FindDefault(string root)
    {
        var files = fileSystem.EnumerateEntries(root).Where(entry => !entry.IsDirectory).Select(entry => entry.FullPath).ToList();
        return First(files, SolutionExtensions) ?? Single(files, ProjectExtensions);
    }

    private static string? First(List<string> files, string[] extensions) =>
        extensions.SelectMany(extension => files.Where(file => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase)).FirstOrDefault();

    private static string? Single(List<string> files, string[] extensions)
    {
        var projects = files.Where(file => HasExtension(file, extensions)).ToList();
        return projects.Count == 1 ? projects[0] : null;
    }
}
