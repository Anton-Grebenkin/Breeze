namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>A file changed in a commit.</summary>
/// <param name="Path">Path relative to the repository root with <c>/</c>.</param>
/// <param name="OriginalPath">Previous path of a renamed or copied file.</param>
public sealed record GitCommitFile(GitFileStatus Status, string Path, string? OriginalPath = null);
