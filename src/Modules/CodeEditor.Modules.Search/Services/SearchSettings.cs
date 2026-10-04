namespace CodeEditor.Modules.Search.Services;

/// <summary>
/// The <c>search</c> settings section, e.g. <c>"search.exclude": { "**/*.min.js": true }</c>; file search only.
/// </summary>
public sealed class SearchSettings
{
    public const string Section = "search";

    /// <summary>Glob → whether to exclude it. Globs set to <c>false</c> are ignored.</summary>
    public Dictionary<string, bool> Exclude { get; set; } = [];
}
