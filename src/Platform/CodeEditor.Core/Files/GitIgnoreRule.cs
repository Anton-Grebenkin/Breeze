using System.Text;
using System.Text.RegularExpressions;

namespace CodeEditor.Core.Files;

/// <summary>
/// One <c>.gitignore</c> rule: <c>*.log</c>, <c>/build</c>, <c>docs/**/*.tmp</c>, <c>!keep.log</c>, <c>out/</c>.
/// </summary>
/// <remarks>
/// Supports the core git syntax: <c>*</c>, <c>?</c>, <c>**</c>, negation <c>!</c>, root anchoring (<c>/</c> at the
/// start or in the middle), directory-only rules (trailing <c>/</c>) and character classes <c>[a-z]</c>, <c>[!a]</c>.
/// The file belongs to someone else's repository, so no line may fail the open: one that cannot be understood is
/// skipped.
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

        try
        {
            var regex = new Regex(
                (anchored ? "^" : "^(?:.*/)?") + GlobToRegex(pattern) + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                MatchTimeout);

            return new GitIgnoreRule(regex, isNegation, directoryOnly);
        }
        catch (ArgumentException)
        {
            // A class the regex rejects ("[z-a]"): git matches nothing with such a line, and the folder still opens.
            return null;
        }
    }

    /// <param name="relativePath">Path from the root with <c>/</c> separators.</param>
    public bool Matches(ReadOnlySpan<char> relativePath, bool isDirectory)
    {
        try
        {
            return (!DirectoryOnly || isDirectory) && _regex.IsMatch(relativePath);
        }
        catch (RegexMatchTimeoutException)
        {
            // A pattern too slow for this path does not hide it; the file tree must not fail on one line.
            return false;
        }
    }

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
            else if (c == '[' && ClassEnd(glob, i) is var end and > 0)
            {
                AppendClass(regex, glob.AsSpan(i + 1, end - i - 1));
                i = end;
            }
            else
            {
                // Includes a "[" that is never closed: a literal, as in git.
                regex.Append(Regex.Escape(c.ToString()));
            }
        }

        return regex.ToString();
    }

    /// <returns>The index of the "]" closing the class opened at <paramref name="start"/>; -1 if it is never closed.</returns>
    private static int ClassEnd(string glob, int start)
    {
        var first = start + 1;
        if (first < glob.Length && glob[first] is '!' or '^')
        {
            first++;
        }

        // The first member may be "]" itself.
        return first + 1 < glob.Length ? glob.IndexOf(']', first + 1) : -1;
    }

    // "[!a-z]" negates like "[^a-z]"; "\" and "[" are plain members in a glob but not in a regex class.
    private static void AppendClass(StringBuilder regex, ReadOnlySpan<char> members)
    {
        regex.Append('[');
        if (members[0] is '!' or '^')
        {
            regex.Append('^');
            members = members[1..];
        }

        foreach (var member in members)
        {
            if (member is '\\' or '[' or ']' or '^')
            {
                regex.Append('\\');
            }

            regex.Append(member);
        }

        regex.Append(']');
    }
}
