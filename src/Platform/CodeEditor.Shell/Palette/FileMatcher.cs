using CodeEditor.Core.Files;
using CodeEditor.Core.Text;

namespace CodeEditor.Shell.Palette;

/// <summary>
/// File matching for <c>Ctrl+P</c>: fuzzy match on the name, then on the relative path; recent files rank higher.
/// </summary>
/// <remarks>O(n) per query over the index snapshot; name matches outweigh path matches.</remarks>
public static class FileMatcher
{
    /// <summary>Users don't scroll past this many rows; they refine the query instead.</summary>
    public const int MaxResults = 500;

    private const int NameMatchBonus = 1000;
    private const int RecentBonus = 50;
    private const int StackBufferLimit = 128;

    /// <param name="recentRank">Path → position in the recent history (0 is the latest).</param>
    public static PaletteItem[] Match(IReadOnlyList<IndexedFile> files, string pattern, IReadOnlyDictionary<string, int> recentRank)
    {
        var matches = new List<(IndexedFile File, int Score, int[] Highlights, bool IsRecent)>();
        Span<int> indices = pattern.Length <= StackBufferLimit ? stackalloc int[pattern.Length] : new int[pattern.Length];

        foreach (var file in files)
        {
            int score;
            int[] highlights;
            if (FuzzyMatcher.TryMatch(pattern, file.Name, indices, out score))
            {
                score += NameMatchBonus;
                highlights = indices.ToArray();
            }
            else if (FuzzyMatcher.TryMatch(pattern, file.RelativePath, out score))
            {
                highlights = [];
            }
            else
            {
                continue;
            }

            var isRecent = recentRank.ContainsKey(file.FullPath);
            if (isRecent)
            {
                score += RecentBonus;
            }

            matches.Add((file, score, highlights, isRecent));
        }

        matches.Sort(static (left, right) => right.Score != left.Score
            ? right.Score.CompareTo(left.Score)
            : left.File.RelativePath.Length.CompareTo(right.File.RelativePath.Length));

        return [.. matches.Take(MaxResults).Select(static match => CreateItem(match.File, match.Highlights, match.IsRecent))];
    }

    /// <summary>Recent files still in the index, newest first: one pass, O(n).</summary>
    public static PaletteItem[] Recent(IReadOnlyList<IndexedFile> files, IReadOnlyDictionary<string, int> recentRank) =>
    [
        .. files.Where(file => recentRank.ContainsKey(file.FullPath))
            .OrderBy(file => recentRank[file.FullPath])
            .Select(file => CreateItem(file, [], isRecent: true)),
    ];

    private static PaletteItem CreateItem(IndexedFile file, int[] highlights, bool isRecent) =>
        new(file.FullPath, file.Name, Shortcut: null, highlights, isRecent)
        {
            Detail = Path.GetDirectoryName(file.RelativePath)?.Replace('\\', '/'),
        };
}
