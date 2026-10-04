using CodeEditor.Core.Text;
using CodeEditor.Shell.Resources;

namespace CodeEditor.Shell.Palette;

/// <summary>
/// A list for <see cref="IQuickPick"/>: original order without a query, fuzzy title search with one.
/// <see cref="CustomItem"/> appends an option built from the typed text ("Use '…'").
/// </summary>
/// <remarks>O(n · m) per filter: n options, m title length.</remarks>
public sealed class QuickPickProvider(string placeholder, IReadOnlyList<QuickPickItem> items, Func<QuickPickItem, Task> accept) : IQuickOpenProvider
{
    private const int StackBufferLimit = 128;

    private readonly Dictionary<string, QuickPickItem> _shown = new(StringComparer.Ordinal);

    public string Prefix => string.Empty;

    public string Placeholder => placeholder;

    public string EmptyText { get; init; } = Strings.NoMatchingItems;

    /// <summary>Builds an option from the typed text; returns <c>null</c> if the text doesn't fit.</summary>
    public Func<string, QuickPickItem?>? CustomItem { get; init; }

    public void Prepare()
    {
    }

    public IReadOnlyList<PaletteItem> Filter(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _shown.Clear();
        var pattern = text.AsSpan().Trim();
        var matches = pattern.IsEmpty ? [.. items.Select(item => (item, 0, Array.Empty<int>()))] : Match(pattern);

        if (CustomItem?.Invoke(text.Trim()) is { } custom && items.All(item => item.Id != custom.Id))
        {
            matches.Add((custom, 0, []));
        }

        var result = new List<PaletteItem>(matches.Count);
        foreach (var (item, _, highlights) in matches)
        {
            _shown[item.Id] = item;
            result.Add(new PaletteItem(item.Id, item.Title, null, highlights, false) { Detail = item.Detail });
        }

        return result;
    }

    public Task AcceptAsync(PaletteItem item, string text)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _shown.TryGetValue(item.Id, out var picked) ? accept(picked) : Task.CompletedTask;
    }

    // Best matches first; ties keep the original order (OrderBy is stable).
    private List<(QuickPickItem Item, int Score, int[] Highlights)> Match(ReadOnlySpan<char> pattern)
    {
        var matches = new List<(QuickPickItem, int, int[])>();
        Span<int> indices = pattern.Length <= StackBufferLimit ? stackalloc int[pattern.Length] : new int[pattern.Length];
        foreach (var item in items)
        {
            if (FuzzyMatcher.TryMatch(pattern, item.Title, indices, out var score))
            {
                matches.Add((item, score, indices.ToArray()));
            }
        }

        return [.. matches.OrderByDescending(match => match.Item2)];
    }
}
