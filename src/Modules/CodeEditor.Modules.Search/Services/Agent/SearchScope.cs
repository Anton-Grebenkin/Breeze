using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Search.Resources;

namespace CodeEditor.Modules.Search.Services.Agent;

/// <summary>
/// Where <c>search_text</c> looks: the folder or file from <c>path</c> (as in ripgrep and Claude Code's Grep) and the
/// <c>include</c> globs. Validated against the index first: an unknown path is an error with similar paths, a glob
/// matching no file is an explicit "nothing searched" rather than "nothing found" (models often write globs like
/// <c>**/Catalog/**</c> for folder <c>src/Acme.Catalog</c>).
/// </summary>
public sealed class SearchScope
{
    private readonly string? _prefix;
    private readonly string _folderPrefix;
    private readonly bool _isFile;
    private readonly GlobFilter _include;

    private SearchScope(string? prefix, bool isFile, GlobFilter include)
    {
        _prefix = prefix;
        _folderPrefix = prefix is null ? string.Empty : prefix + "/";
        _isFile = isFile;
        _include = include;
    }

    /// <param name="relativePath">Workspace-relative path with <c>/</c>; <c>null</c> for the whole workspace.</param>
    /// <exception cref="AgentToolException">The file or folder is not in the index.</exception>
    public static SearchScope Create(IReadOnlyList<IndexedFile> files, string? relativePath, string? path, string? include)
    {
        ArgumentNullException.ThrowIfNull(files);
        var filter = GlobFilter.Parse(include);
        var prefix = relativePath?.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(prefix) || prefix == ".")
        {
            return new SearchScope(null, isFile: false, filter);
        }

        if (files.Any(file => file.RelativePath.Equals(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return new SearchScope(prefix, isFile: true, filter);
        }

        var folderPrefix = prefix + "/";
        if (files.Any(file => file.RelativePath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            return new SearchScope(prefix, isFile: false, filter);
        }

        throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.ToolPathNotFound, path) + Similar(files, prefix));
    }

    public bool Contains(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var inPath = _prefix is null
            || (_isFile
                ? relativePath.Equals(_prefix, StringComparison.OrdinalIgnoreCase)
                : relativePath.StartsWith(_folderPrefix, StringComparison.OrdinalIgnoreCase));
        return inPath && (_include.IsEmpty || _include.Matches(relativePath));
    }

    /// <returns>"Nothing searched" with similar paths if no file is in scope; otherwise <c>null</c>.</returns>
    public string? EmptyScopeMessage(IReadOnlyList<IndexedFile> files, string? include)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Any(file => Contains(file.RelativePath)))
        {
            return null;
        }

        var where = _prefix is null ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.ToolInPath, _prefix);
        return string.Format(CultureInfo.CurrentCulture, Strings.ToolIncludeMatchesNothing, include, where) + Similar(files, include ?? string.Empty);
    }

    private static string Similar(IReadOnlyList<IndexedFile> files, string pattern)
    {
        var similar = SimilarPaths.Suggest(files.Select(file => file.RelativePath), pattern);
        return similar.Count == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.ToolSimilarPaths, string.Join(", ", similar));
    }
}
