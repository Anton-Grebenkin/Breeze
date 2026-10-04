namespace CodeEditor.Core.Files;

/// <summary>
/// Indexed workspace file: full path, root-relative path (with <c>/</c>) and name, for fast lookup.
/// </summary>
public readonly record struct IndexedFile(string FullPath, string RelativePath, string Name);
