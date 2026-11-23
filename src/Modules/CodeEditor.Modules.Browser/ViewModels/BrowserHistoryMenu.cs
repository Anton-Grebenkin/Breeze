using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Shell.Menus;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Browser.ViewModels;

/// <summary>The address bar's history menu: the latest pages, then "all pages" and "clear".</summary>
internal static class BrowserHistoryMenu
{
    /// <summary>Pages in the menu; the rest are in the palette.</summary>
    public const int Size = 15;

    private const int MaxLabel = 90;

    public static IReadOnlyList<MenuItemViewModel> Build(IReadOnlyList<BrowserVisit> visits, Func<Uri, Task> open, IAsyncRelayCommand showAll, IAsyncRelayCommand clear)
    {
        if (visits.Count == 0)
        {
            return [MenuItemViewModel.ForAction(Strings.HistoryEmpty, "Browser.History.Empty", new AsyncRelayCommand(() => Task.CompletedTask, () => false))];
        }

        return
        [
            .. visits.Take(Size).Select((visit, index) =>
                MenuItemViewModel.ForAction(Label(visit), "Browser.History.Page" + index, new AsyncRelayCommand(() => open(new Uri(visit.Url))))),
            MenuItemViewModel.CreateSeparator(),
            MenuItemViewModel.ForAction(Strings.AllPages, "Browser.History.All", showAll),
            MenuItemViewModel.ForAction(Strings.ClearHistory, "Browser.History.Clear", clear),
        ];
    }

    /// <summary>"Title — localhost:5000/orders": history rows are found by either.</summary>
    public static string Label(BrowserVisit visit)
    {
        var address = Uri.TryCreate(visit.Url, UriKind.Absolute, out var url) ? url.Authority + url.PathAndQuery.TrimEnd('/') : visit.Url;
        var label = visit.Title.Length > 0 ? visit.Title + " — " + address : address;
        return label.Length <= MaxLabel ? label : string.Concat(label.AsSpan(0, MaxLabel), "…");
    }
}
