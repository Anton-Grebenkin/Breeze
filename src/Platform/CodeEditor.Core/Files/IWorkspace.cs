namespace CodeEditor.Core.Files;

/// <summary>
/// The open workspace folder. Modules and the agent work within its bounds.
/// </summary>
public interface IWorkspace
{
    /// <summary>Context key for <c>when</c> conditions: a folder is open.</summary>
    const string OpenContextKey = "workspaceOpen";

    /// <summary>Full folder path, or <c>null</c> when no folder is open.</summary>
    string? Root { get; }

    /// <summary>Folder name for the window title.</summary>
    string? Name { get; }

    /// <summary>
    /// A folder was opened or closed. Raised on the thread that called <see cref="Open"/> or <see cref="Close"/>.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>Raised in batches on a background thread.</summary>
    event EventHandler<FileChangesEventArgs>? FilesChanged;

    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    void Open(string folder);

    void Close();

    /// <summary>
    /// User patterns (<c>files.exclude</c>) on top of tool folders and <c>.gitignore</c>. A change while a folder is
    /// open raises <see cref="FilesChanged"/> with a full rescan.
    /// </summary>
    void SetExcludePatterns(GlobFilter patterns);

    /// <summary>
    /// Whether the path is hidden: outside the folder, a tool folder, a <c>.gitignore</c> rule or a
    /// <c>files.exclude</c> pattern.
    /// </summary>
    bool IsExcluded(string fullPath, bool isDirectory);

    /// <summary>Path relative to the root with <c>/</c> separators.</summary>
    string RelativePath(string fullPath);
}
