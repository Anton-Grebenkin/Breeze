namespace CodeEditor.Core.Files;

/// <summary>
/// Comma-separated globs, as in VS Code search fields: <c>*.cs, src/**, docs/, *.{json,xml}</c>. A path matches when
/// it or any of its folders matches a pattern: <c>src</c> covers everything inside <c>src</c>.
/// </summary>
/// <remarks>Syntax follows <c>.gitignore</c>: without <c>/</c> at any depth, with <c>/</c> from the root.</remarks>
public sealed class GlobFilter
{
    private readonly GitIgnoreRule[] _rules;

    private GlobFilter(GitIgnoreRule[] rules) => _rules = rules;

    public static GlobFilter Empty { get; } = new([]);

    public bool IsEmpty => _rules.Length == 0;

    public static GlobFilter Parse(string? patterns) =>
        string.IsNullOrWhiteSpace(patterns)
            ? Empty
            : Create(GlobBraces.Split(patterns).Select(pattern => pattern.Trim()).Where(pattern => pattern.Length > 0));

    /// <summary>One pattern per item (<c>files.exclude</c> keys). Braces <c>{a,b}</c> are expanded.</summary>
    public static GlobFilter Create(IEnumerable<string> patterns) =>
        new([
            .. patterns
                .SelectMany(pattern => GlobBraces.Expand(pattern.Trim()))
                .Select(pattern => GitIgnoreRule.Parse(pattern.StartsWith("./", StringComparison.Ordinal) ? pattern[1..] : pattern))
                .OfType<GitIgnoreRule>()
                .Where(rule => !rule.IsNegation),
        ]);

    /// <summary>Whether the file or one of its folders matches. O(depth × patterns).</summary>
    /// <param name="relativePath">Path from the root with <c>/</c> separators.</param>
    /// <param name="isDirectory">The path is a folder, so directory-only patterns (<c>logs/</c>) apply too.</param>
    public bool Matches(string relativePath, bool isDirectory = false)
    {
        var end = relativePath.Length;
        while (end > 0)
        {
            var prefix = relativePath.AsSpan(0, end);
            foreach (var rule in _rules)
            {
                if (rule.Matches(prefix, isDirectory))
                {
                    return true;
                }
            }

            end = relativePath.LastIndexOf('/', end - 1);
            isDirectory = true;
        }

        return false;
    }
}
