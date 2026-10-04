using System.Collections.Frozen;

namespace CodeEditor.Core.Files;

/// <summary>
/// What to hide in the workspace: tool folders (<c>.git</c>, <c>bin</c>, <c>obj</c>, <c>node_modules</c>…)
/// and the rules of the root <c>.gitignore</c>.
/// </summary>
public sealed class PathExclusions
{
    public const string GitIgnoreFileName = ".gitignore";

    /// <summary>Tool folders that are almost never needed in a code editor.</summary>
    public static readonly FrozenSet<string> DefaultExcludedNames = new[]
    {
        ".git", ".vs", ".idea", "bin", "obj", "node_modules",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly GitIgnoreRule[] _rules;

    private PathExclusions(GitIgnoreRule[] rules) => _rules = rules;

    public static PathExclusions Empty { get; } = new([]);

    public static PathExclusions FromGitIgnore(string content) =>
        new([.. content.Split('\n').Select(line => GitIgnoreRule.Parse(line.TrimEnd('\r'))).OfType<GitIgnoreRule>()]);

    /// <summary>Reads the root <c>.gitignore</c>; without it only the default exclusions apply.</summary>
    public static PathExclusions Load(IFileSystem fileSystem, string root)
    {
        var gitIgnore = Path.Combine(root, GitIgnoreFileName);
        return fileSystem.FileExists(gitIgnore) ? FromGitIgnore(fileSystem.ReadAllText(gitIgnore)) : Empty;
    }

    /// <summary>
    /// Whether the path or any of its parent folders is excluded. O(depth × rules).
    /// </summary>
    /// <param name="relativePath">Path from the workspace root, any separators.</param>
    public bool IsExcluded(string relativePath, bool isDirectory)
    {
        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Each parent folder path is a prefix of the joined path, so one join serves all segments.
        var normalized = string.Join('/', segments);
        var prefixLength = 0;
        for (var i = 0; i < segments.Length; i++)
        {
            prefixLength += (i == 0 ? 0 : 1) + segments[i].Length;
            var segmentIsDirectory = i < segments.Length - 1 || isDirectory;
            if (DefaultExcludedNames.Contains(segments[i]) && segmentIsDirectory)
            {
                return true;
            }

            if (MatchesRules(normalized.AsSpan(0, prefixLength), segmentIsDirectory))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>As in git: the last matching rule wins, and a negation re-includes the path.</summary>
    private bool MatchesRules(ReadOnlySpan<char> relativePath, bool isDirectory)
    {
        var excluded = false;
        foreach (var rule in _rules)
        {
            if (rule.Matches(relativePath, isDirectory))
            {
                excluded = !rule.IsNegation;
            }
        }

        return excluded;
    }
}
