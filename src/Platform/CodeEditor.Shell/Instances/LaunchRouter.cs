using CodeEditor.Core.Files;

namespace CodeEditor.Shell.Instances;

/// <summary>
/// Decides where a launch goes, as VS Code does: a folder that is open in a window activates it; a file goes to the
/// window whose folder contains it, else to the most recently active window; with no path a new empty window opens,
/// and only the first window restores the previous folder.
/// </summary>
public static class LaunchRouter
{
    /// <param name="others">The other running windows, the most recently active first.</param>
    public static LaunchPlan Plan(LaunchRequest request, IFileSystem fileSystem, IReadOnlyList<WindowEntry> others)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(others);
        if (request.Path is { } path && fileSystem.FileExists(path))
        {
            var window = others.FirstOrDefault(other => other.Folder is { } folder && Contains(folder, path))
                ?? (request.NewWindow ? null : others.FirstOrDefault());
            return new LaunchPlan { Target = window, File = path };
        }

        // A path that is neither a file nor a folder is opened as a folder, which reports it as not found.
        if (request.Path is { } folderPath)
        {
            return new LaunchPlan { Target = others.FirstOrDefault(other => SameFolder(other.Folder, folderPath)), Folder = folderPath };
        }

        return new LaunchPlan { RestoreLastSession = !request.NewWindow && others.Count == 0 };
    }

    public static bool SameFolder(string? folder, string other) =>
        folder is not null && string.Equals(Normalize(folder), Normalize(other), StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string folder, string file) =>
        file.StartsWith(Normalize(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
