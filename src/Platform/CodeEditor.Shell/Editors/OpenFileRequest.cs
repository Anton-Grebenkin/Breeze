namespace CodeEditor.Shell.Editors;

/// <summary>
/// Argument of the "Open file" command. <see cref="Preview"/> opens a preview tab, like a single click in VS Code:
/// the next preview replaces it while it has no edits.
/// </summary>
public sealed record OpenFileRequest(string FilePath, bool Preview = false);
