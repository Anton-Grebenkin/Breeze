using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// Command-line tools on the machine, as a line for the folder snapshot, so the model does not waste steps on tools
/// that are missing. Searched on PATH without starting processes, once per session in the background
/// (<see cref="ExecutablePaths"/>). The Microsoft Store python alias (which opens the Store) does not count as Python.
/// </summary>
/// <param name="path">Directories to search; <c>null</c> for the PATH variable.</param>
/// <param name="extensions">Executable extensions; <c>null</c> for the PATHEXT variable.</param>
public sealed class EnvironmentTools(IFileSystem fileSystem, string? path = null, string? extensions = null)
{
    private const string StoreAliases = @"\Microsoft\WindowsApps";

    private static readonly string[] Candidates = ["dotnet", "git", "node", "npm", "python", "pwsh", "java", "docker"];

    private readonly Lazy<Task<string?>> _line = new(() => Task.Run(() => Describe(fileSystem, path, extensions)));

    /// <summary>"Tools on PATH: …; not found: …", or <c>null</c> if nothing was found (empty PATH).</summary>
    public Task<string?> DescribeAsync() => _line.Value;

    private static string? Describe(IFileSystem fileSystem, string? path, string? extensions)
    {
        var found = Candidates
            .Where(tool => ExecutablePaths.Find(fileSystem, tool, path, extensions).Any(file => !(tool == "python" && IsStoreAlias(file))))
            .ToList();
        if (found.Count == 0)
        {
            return null;
        }

        var missing = Candidates.Except(found).ToList();
        return "Tools on PATH: " + string.Join(", ", found) + (missing.Count > 0 ? "; not found: " + string.Join(", ", missing) : string.Empty) + ".";
    }

    private static bool IsStoreAlias(string file) =>
        Path.GetDirectoryName(file) is { } folder && Path.TrimEndingDirectorySeparator(folder).EndsWith(StoreAliases, StringComparison.OrdinalIgnoreCase);
}
