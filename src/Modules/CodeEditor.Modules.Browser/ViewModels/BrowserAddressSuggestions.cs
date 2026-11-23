using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Tabs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Browser.ViewModels;

/// <summary>
/// What the address bar offers while the user types, as browsers do: pages from the history and known sites that match
/// by address or title. An address that starts with the typed text comes first, then one that contains it, then a
/// title; within each, the most recent first. The arrows choose, Enter opens, Escape closes. A scan of at most a few
/// hundred entries per keystroke.
/// </summary>
public sealed partial class BrowserAddressSuggestions(BrowserHistory? history = null, BrowserKnownSites? sites = null) : ObservableObject
{
    public const int MaxItems = 8;

    private static readonly string[] Prefixes = ["https://", "http://", "www."];

    [ObservableProperty]
    public partial IReadOnlyList<BrowserSuggestion> Items { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>The chosen suggestion; -1 while the typed text itself is what Enter opens.</summary>
    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>The user typed into the bar.</summary>
    public void Update(string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var text = Bare(typed.Trim());
        Items = text.Length == 0 ? [] : Find(text);
        SelectedIndex = -1;
        IsOpen = Items.Count > 0;
    }

    /// <summary>The chosen suggestion, if any; the list closes either way.</summary>
    public BrowserSuggestion? Take()
    {
        var chosen = IsOpen && SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;
        Dismiss();
        return chosen;
    }

    /// <summary>The user pointed at a suggestion with the mouse.</summary>
    public void Choose(BrowserSuggestion suggestion) => SelectedIndex = Array.IndexOf([.. Items], suggestion);

    [RelayCommand]
    private void Dismiss() => (IsOpen, SelectedIndex) = (false, -1);

    [RelayCommand]
    private void Next() => Move(step: 1);

    [RelayCommand]
    private void Previous() => Move(step: -1);

    // Around the list and through "nothing chosen", so the typed text can be opened again.
    private void Move(int step)
    {
        if (IsOpen)
        {
            var positions = Items.Count + 1;
            SelectedIndex = (SelectedIndex + 1 + step + positions) % positions - 1;
        }
    }

    private List<BrowserSuggestion> Find(string text)
    {
        var matches = new List<(int Rank, BrowserSuggestion Item)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var visit in history?.Visits ?? [])
        {
            var address = Bare(visit.Url);
            if (Rank(address, visit.Title, text) is var rank and >= 0 && seen.Add(address))
            {
                matches.Add((rank, new BrowserSuggestion(visit.Url, visit.Title, address)));
            }
        }

        foreach (var host in sites?.Hosts ?? [])
        {
            if (Rank(host, string.Empty, text) is var rank and >= 0 && seen.Add(Bare(host)))
            {
                matches.Add((rank, new BrowserSuggestion("https://" + host + "/", string.Empty, Bare(host))));
            }
        }

        // The sort is stable: the history's order, most recent first, holds within a rank.
        return [.. matches.OrderBy(static match => match.Rank).Take(MaxItems).Select(static match => match.Item)];
    }

    private static int Rank(string address, string title, string text) =>
        Bare(address).StartsWith(text, StringComparison.OrdinalIgnoreCase) ? 0
        : address.Contains(text, StringComparison.OrdinalIgnoreCase) ? 1
        : title.Contains(text, StringComparison.OrdinalIgnoreCase) ? 2
        : -1;

    // "https://www.example.org/docs/" → "example.org/docs".
    private static string Bare(string address)
    {
        foreach (var prefix in Prefixes)
        {
            if (address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                address = address[prefix.Length..];
            }
        }

        return address.TrimEnd('/');
    }
}
