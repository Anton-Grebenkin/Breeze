namespace CodeEditor.Core.Files;

/// <summary>
/// Similar paths for a glob or path that matched no files, so the agent does not search blindly: "Catalog" gives
/// <c>src/Acme.Catalog</c>, "Migrations/Snapshot.cs" gives the file's full path. Literal parts of the pattern are compared
/// with folder and file names case-insensitively: exact name first, then substring. O(files × depth).
/// </summary>
public static class SimilarPaths
{
    public const int DefaultMax = 5;

    private static readonly char[] Wildcards = ['*', '?', '{', '['];

    /// <param name="files">Root-relative file paths with <c>/</c> separators.</param>
    /// <param name="pattern">A glob (<c>**/Catalog/**/*.cs</c>) or a path (<c>Catalog/Migrations</c>).</param>
    public static IReadOnlyList<string> Suggest(IEnumerable<string> files, string pattern, int max = DefaultMax)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(pattern);
        var segments = LiteralSegments(pattern);
        if (segments.Count == 0)
        {
            return [];
        }

        var paths = files.ToList();
        var fileName = segments[^1].Contains('.', StringComparison.Ordinal) ? segments[^1] : null;
        var suggestions = new List<string>();
        if (fileName is not null)
        {
            suggestions.AddRange(Ranked(paths, fileName));
        }

        var folders = Folders(paths);
        foreach (var segment in segments.Where(segment => segment != fileName))
        {
            suggestions.AddRange(Ranked(folders, segment));
        }

        return [.. suggestions.Distinct(StringComparer.Ordinal).Take(max)];
    }

    // Exact name, then a name part ("Catalog" in "Acme.Catalog") or prefix; a substring inside a word ("log" in
    // "Dialog") only when nothing better exists. Shorter paths come first.
    private static IEnumerable<string> Ranked(IEnumerable<string> paths, string segment)
    {
        var candidates = paths
            .Select(path => (Path: path, Rank: Rank(path[(path.LastIndexOf('/') + 1)..], segment)))
            .Where(entry => entry.Rank < NoMatch)
            .ToList();
        var best = candidates.Count == 0 ? NoMatch : candidates.Min(entry => entry.Rank);
        var cutoff = best < Substring ? Substring : NoMatch;
        return candidates
            .Where(entry => entry.Rank < cutoff)
            .OrderBy(entry => entry.Rank)
            .ThenBy(entry => entry.Path.Count(character => character == '/'))
            .ThenBy(entry => entry.Path, StringComparer.Ordinal)
            .Select(entry => entry.Path);
    }

    private const int Exact = 0;
    private const int Part = 1;
    private const int Substring = 2;
    private const int NoMatch = 3;

    private static readonly char[] NameSeparators = ['.', '-', '_', ' '];

    private static int Rank(string name, string segment)
    {
        if (name.Equals(segment, StringComparison.OrdinalIgnoreCase))
        {
            return Exact;
        }

        if (name.StartsWith(segment, StringComparison.OrdinalIgnoreCase)
            || name.Split(NameSeparators).Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase)))
        {
            return Part;
        }

        return name.Contains(segment, StringComparison.OrdinalIgnoreCase) ? Substring : NoMatch;
    }

    private static List<string> LiteralSegments(string pattern) =>
    [
        .. pattern.Replace('\\', '/').Split(['/', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => segment is not "." and not ".." && segment.IndexOfAny(Wildcards) < 0),
    ];

    private static HashSet<string> Folders(IEnumerable<string> files)
    {
        var folders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            for (var end = file.LastIndexOf('/'); end > 0 && folders.Add(file[..end]); end = file.LastIndexOf('/', end - 1))
            {
            }
        }

        return folders;
    }
}
