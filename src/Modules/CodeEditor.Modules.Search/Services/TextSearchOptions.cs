using CodeEditor.Modules.Search.Services.Matching;

namespace CodeEditor.Modules.Search.Services;

/// <summary>
/// File search query, as in VS Code: text or regex, case, whole word, and comma-separated include and exclude globs
/// (<c>*.cs, src/</c>).
/// </summary>
public sealed record TextSearchOptions(string Pattern)
{
    public bool MatchCase { get; init; }

    public bool WholeWord { get; init; }

    public bool UseRegex { get; init; }

    public string? Include { get; init; }

    public string? Exclude { get; init; }

    /// <summary>Extra filter by relative path (the agent's search scope); files outside it are not read.</summary>
    public Func<string, bool>? Filter { get; init; }

    /// <summary>Chars kept before the match in previews: the panel needs few, the agent needs the whole line.</summary>
    public int PreviewLead { get; init; } = MatchCollector.PreviewLead;
}
