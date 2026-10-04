namespace CodeEditor.Core.Files;

/// <summary>
/// File or folder change. A rename arrives as a pair: deletion of the old path and creation of the new one.
/// </summary>
public readonly record struct FileChange(string Path, FileChangeKind Kind);
