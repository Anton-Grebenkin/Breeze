namespace CodeEditor.Core.Files;

/// <summary>
/// Folder entry: a file or a subfolder.
/// </summary>
public readonly record struct FileSystemEntry(string Name, string FullPath, bool IsDirectory);
