using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Resources;

namespace CodeEditor.Modules.Agent.Contracts.Files;

/// <summary>Tool paths: relative to the workspace folder and confined to it.</summary>
public static class WorkspacePaths
{
    /// <summary>Full path for a relative path (or a full path inside the folder).</summary>
    /// <exception cref="AgentToolException">No folder is open, or the path leads outside.</exception>
    public static string Resolve(IWorkspace workspace, string? relativePath)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var root = workspace.Root ?? throw new AgentToolException(Strings.WorkspaceNotOpen);
        var path = string.IsNullOrWhiteSpace(relativePath) || relativePath is "." or "/" ? root : relativePath.Trim();

        var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
        if (!IsUnder(root, full) || LeadsOutside(root, full))
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.PathOutsideWorkspace, relativePath));
        }

        return full;
    }

    private static bool IsUnder(string root, string full)
    {
        var relative = Path.GetRelativePath(root, full);
        return !(relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative));
    }

    // A junction or symbolic link inside the folder may point anywhere: the boundary is where the path really leads.
    // One attribute read per path part below the root; a part that does not exist yet has nothing to follow.
    private static bool LeadsOutside(string root, string full)
    {
        for (var current = full; current.Length > root.Length; current = Path.GetDirectoryName(current) ?? root)
        {
            if (TargetOf(current) is { } target && !IsUnder(root, target) && !(TargetOf(root) is { } realRoot && IsUnder(realRoot, target)))
            {
                return true;
            }
        }

        return false;
    }

    /// <returns>Where a link finally leads; <c>null</c> if the path is not a link or cannot be examined.</returns>
    private static string? TargetOf(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return info.Exists && info.LinkTarget is not null ? info.ResolveLinkTarget(returnFinalTarget: true)?.FullName : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
