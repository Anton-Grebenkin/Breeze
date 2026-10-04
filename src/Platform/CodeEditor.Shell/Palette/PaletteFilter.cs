using CodeEditor.Core.Text;

namespace CodeEditor.Shell.Palette;

/// <summary>
/// Palette filtering and sorting. Empty query: recent commands first, then alphabetical. Otherwise fuzzy match on
/// "Category: Title" by descending score; ties go to recent commands.
/// </summary>
/// <remarks>O(n · m) matching (n commands, m title length) plus O(k log k) to sort k matches.</remarks>
public static class PaletteFilter
{
    private const int StackBufferLimit = 128;

    private static readonly StringComparer TitleComparer = StringComparer.InvariantCultureIgnoreCase;

    public static IReadOnlyList<PaletteItem> Apply(
        IReadOnlyList<PaletteCandidate> candidates,
        string query,
        IReadOnlyList<string> recentCommandIds)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(recentCommandIds);

        var recentRank = RankRecent(recentCommandIds);
        var pattern = query.AsSpan().Trim();

        return pattern.IsEmpty
            ? ListAll(candidates, recentRank)
            : Match(candidates, pattern, recentRank);
    }

    private static PaletteItem[] ListAll(IReadOnlyList<PaletteCandidate> candidates, Dictionary<string, int> recentRank)
    {
        var items = new PaletteItem[candidates.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = CreateItem(candidates[i], [], recentRank);
        }

        Array.Sort(items, (left, right) =>
        {
            var byRecent = RankOf(left, recentRank).CompareTo(RankOf(right, recentRank));
            return byRecent != 0 ? byRecent : TitleComparer.Compare(left.Title, right.Title);
        });

        return items;
    }

    private static PaletteItem[] Match(
        IReadOnlyList<PaletteCandidate> candidates,
        ReadOnlySpan<char> pattern,
        Dictionary<string, int> recentRank)
    {
        var matches = new List<(PaletteItem Item, int Score)>();
        Span<int> indices = pattern.Length <= StackBufferLimit ? stackalloc int[pattern.Length] : new int[pattern.Length];

        foreach (var candidate in candidates)
        {
            if (FuzzyMatcher.TryMatch(pattern, candidate.Command.DisplayTitle, indices, out var score))
            {
                matches.Add((CreateItem(candidate, indices.ToArray(), recentRank), score));
            }
        }

        matches.Sort((left, right) =>
        {
            var byScore = right.Score.CompareTo(left.Score);
            if (byScore != 0)
            {
                return byScore;
            }

            var byRecent = RankOf(left.Item, recentRank).CompareTo(RankOf(right.Item, recentRank));
            return byRecent != 0 ? byRecent : TitleComparer.Compare(left.Item.Title, right.Item.Title);
        });

        return [.. matches.Select(match => match.Item)];
    }

    private static PaletteItem CreateItem(PaletteCandidate candidate, int[] highlights, Dictionary<string, int> recentRank) =>
        new(candidate.Command.Id,
            candidate.Command.DisplayTitle,
            candidate.Shortcut,
            highlights,
            recentRank.ContainsKey(candidate.Command.Id));

    private static int RankOf(PaletteItem item, Dictionary<string, int> recentRank) =>
        recentRank.GetValueOrDefault(item.Id, int.MaxValue);

    private static Dictionary<string, int> RankRecent(IReadOnlyList<string> recentCommandIds)
    {
        var ranks = new Dictionary<string, int>(recentCommandIds.Count, StringComparer.Ordinal);
        for (var i = 0; i < recentCommandIds.Count; i++)
        {
            ranks.TryAdd(recentCommandIds[i], i);
        }

        return ranks;
    }
}
