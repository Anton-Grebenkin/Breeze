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
        var relative = Path.GetRelativePath(root, full);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.PathOutsideWorkspace, relativePath));
        }

        return full;
    }
}
