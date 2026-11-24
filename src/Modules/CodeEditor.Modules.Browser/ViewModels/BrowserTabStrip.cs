using System.Collections.ObjectModel;
using CodeEditor.Modules.Browser.Services.Tabs;

namespace CodeEditor.Modules.Browser.ViewModels;

/// <summary>The strip's items kept in line with the browser's tabs. UI thread only.</summary>
public sealed class BrowserTabStrip(BrowserTabs tabs)
{
    public ObservableCollection<BrowserTabViewModel> Items { get; } = [];

    /// <summary>
    /// Closed tabs leave, new ones appear in place, moved ones move, so the items of unchanged tabs stay as they are.
    /// O(n²) over a handful of tabs.
    /// </summary>
    /// <returns>The shown tab's item.</returns>
    public BrowserTabViewModel? Sync()
    {
        var current = tabs.Tabs;
        for (var index = Items.Count - 1; index >= 0; index--)
        {
            if (!current.Contains(Items[index].Tab))
            {
                Items.RemoveAt(index);
            }
        }

        for (var index = 0; index < current.Length; index++)
        {
            var at = IndexOf(current[index]);
            if (at < 0)
            {
                Items.Insert(index, new BrowserTabViewModel(current[index], tabs));
            }
            else if (at != index)
            {
                Items.Move(at, index);
            }
        }

        var active = tabs.Active;
        foreach (var item in Items)
        {
            item.IsActive = item.Tab == active;
            item.Refresh();
        }

        return Items.FirstOrDefault(static item => item.IsActive);
    }

    public void Refresh(BrowserTab tab)
    {
        var at = IndexOf(tab);
        if (at >= 0)
        {
            Items[at].Refresh();
        }
    }

    private int IndexOf(BrowserTab tab)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].Tab == tab)
            {
                return index;
            }
        }

        return -1;
    }
}
