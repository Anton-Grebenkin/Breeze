namespace CodeEditor.Core.Files;

/// <summary>
/// Brace expansion in globs, as in ripgrep and VS Code: <c>*.{cs,sql}</c> means <c>*.cs</c> and <c>*.sql</c>.
/// A comma inside braces does not split a comma-separated pattern list.
/// </summary>
public static class GlobBraces
{
    /// <summary>Splits a pattern list on top-level commas: <c>"*.{cs,sql}, docs/"</c> gives two patterns.</summary>
    public static IEnumerable<string> Split(string patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        var depth = 0;
        var start = 0;
        for (var index = 0; index < patterns.Length; index++)
        {
            depth += patterns[index] switch { '{' => 1, '}' when depth > 0 => -1, _ => 0 };
            if (patterns[index] == ',' && depth == 0)
            {
                yield return patterns[start..index];
                start = index + 1;
            }
        }

        yield return patterns[start..];
    }

    /// <summary>
    /// Expands braces, nested ones too: <c>src/{a,b}/*.{cs,md}</c> gives four patterns. An unmatched brace stays as is.
    /// </summary>
    public static IEnumerable<string> Expand(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var open = pattern.IndexOf('{', StringComparison.Ordinal);
        var close = open < 0 ? -1 : MatchingClose(pattern, open);
        if (close < 0)
        {
            return [pattern];
        }

        var head = pattern[..open];
        var tail = pattern[(close + 1)..];
        return Split(pattern[(open + 1)..close])
            .SelectMany(option => Expand(head + option + tail))
            .Distinct(StringComparer.Ordinal);
    }

    private static int MatchingClose(string pattern, int open)
    {
        var depth = 0;
        for (var index = open; index < pattern.Length; index++)
        {
            depth += pattern[index] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return index;
            }
        }

        return -1;
    }
}
