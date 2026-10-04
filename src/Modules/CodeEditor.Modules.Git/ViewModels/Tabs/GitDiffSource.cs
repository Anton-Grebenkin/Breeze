using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.ViewModels.Tabs;

/// <summary>What a diff tab shows: title, root-relative path, the file on disk and how to load the rows.</summary>
/// <param name="FilePath">Full path for "Open File"; <c>null</c> when the file is gone (deleted).</param>
public sealed record GitDiffSource(string Title, string Path, string? FilePath, Func<CancellationToken, Task<IReadOnlyList<GitDiffRow>>> Load);
