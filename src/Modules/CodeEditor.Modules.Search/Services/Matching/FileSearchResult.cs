namespace CodeEditor.Modules.Search.Services.Matching;

/// <summary>Matches in one file, in text order.</summary>
/// <param name="RelativePath">Workspace-relative path with <c>/</c> separators.</param>
public sealed record FileSearchResult(string FullPath, string RelativePath, IReadOnlyList<SearchMatch> Matches);
