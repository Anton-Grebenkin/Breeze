using System.Text;
using System.Text.RegularExpressions;

namespace CodeEditor.Core.Files;

/// <summary>
/// One <c>.gitignore</c> rule: <c>*.log</c>, <c>/build</c>, <c>docs/**/*.tmp</c>, <c>!keep.log</c>, <c>out/</c>.
/// </summary>
/// <remarks>
/// Supports the core git syntax: <c>*</c>, <c>?</c>, <c>**</c>, negation <c>!</c>, root anchoring (<c>/</c> at the
/// start or in the middle) and directory-only rules (trailing <c>/</c>). Character classes <c>[...]</c> are passed
/// to the regex as is.
/// </remarks>
internal sealed class GitIgnoreRule
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private readonly Regex _regex;

    private GitIgnoreRule(Regex regex, bool isNegation, bool directoryOnly)
    {
        _regex = regex;
        IsNegation = isNegation;
        DirectoryOnly = directoryOnly;
    }

    public bool IsNegation { get; }

    public bool DirectoryOnly { get; }

    /// <summary>Parses one line; empty lines and comments yield <c>null</c>.</summary>
    public static GitIgnoreRule? Parse(string line)
    {
        var pattern = line.TrimEnd();
        if (pattern.Length == 0 || pattern[0] == '#')
        {
            return null;
        }

        var isNegation = pattern[0] == '!';
        if (isNegation)
        {
            pattern = pattern[1..];
        }

        var directoryOnly = pattern.EndsWith('/');
        pattern = pattern.TrimEnd('/');

        // A slash at the start or in the middle anchors the rule to the root; otherwise it applies at any depth.
        var anchored = pattern.Contains('/');
        pattern = pattern.TrimStart('/');
        if (pattern.Length == 0)
        {
            return null;
        }

        var regex = new Regex(
            (anchored ? "^" : "^(?:.*/)?") + GlobToRegex(pattern) + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            MatchTimeout);

        return new GitIgnoreRule(regex, isNegation, directoryOnly);
    }

    /// <param name="relativePath">Path from the root with <c>/</c> separators.</param>
    public bool Matches(ReadOnlySpan<char> relativePath, bool isDirectory) =>
        (!DirectoryOnly || isDirectory) && _regex.IsMatch(relativePath);

    private static string GlobToRegex(string glob)
    {
        var regex = new StringBuilder(glob.Length * 2);
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                // "**/" matches any folders (including none); a trailing "**" matches everything inside.
                var followedBySlash = i + 2 < glob.Length && glob[i + 2] == '/';
                regex.Append(followedBySlash ? "(?:.*/)?" : ".*");
                i += followedBySlash ? 2 : 1;
            }
            else if (c == '*')
            {
                regex.Append("[^/]*");
            }
            else if (c == '?')
            {
                regex.Append("[^/]");
            }
            else if (c == '[')
            {
                regex.Append('[');
            }
            else if (c == ']')
            {
                regex.Append(']');
            }
            else
            {
                regex.Append(Regex.Escape(c.ToString()));
            }
        }

        return regex.ToString();
    }
}
