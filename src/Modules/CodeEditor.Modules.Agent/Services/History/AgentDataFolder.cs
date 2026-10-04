using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Files;

namespace CodeEditor.Modules.Agent.Services.History;

/// <summary>
/// The agent data folder in the workspace, <c>.breeze/agent/</c>: chat history, tool outputs, memory (ADR 0012). It holds
/// a <c>.gitignore</c> with <c>*</c>, so the data stays out of the repository whichever feature creates the folder first.
/// </summary>
public static class AgentDataFolder
{
    private const string GitIgnore = ".gitignore";

    /// <summary>Full path of the data folder or a subfolder; <c>null</c> without an open folder.</summary>
    public static string? PathOf(IWorkspace workspace, params string[] children)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.Root is { } root
            ? Path.Combine([root, .. SensitivePaths.AgentDataFolder.Split('/'), .. children])
            : null;
    }

    /// <summary>Creates the data folder (and the subfolder) with <c>.gitignore</c> if missing.</summary>
    /// <param name="folder">The data folder or a subfolder.</param>
    public static void Ensure(IWorkspace workspace, IFileSystem fileSystem, string folder)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (PathOf(workspace) is not { } root)
        {
            return;
        }

        if (!fileSystem.DirectoryExists(root))
        {
            fileSystem.CreateDirectory(root);
        }

        var gitIgnore = Path.Combine(root, GitIgnore);
        if (!fileSystem.FileExists(gitIgnore))
        {
            fileSystem.WriteAllBytesAtomic(gitIgnore, "*\n"u8.ToArray());
        }

        if (!fileSystem.DirectoryExists(folder))
        {
            fileSystem.CreateDirectory(folder);
        }
    }
}
