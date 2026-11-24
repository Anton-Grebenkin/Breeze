using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Shell.Menus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Browser.ViewModels;

/// <summary>
/// A tab in the browser's strip: the site icon, the page title, a mark on the agent's tab, close and pin. A pinned tab
/// shows only its icon. The middle mouse button closes a tab; the same actions are in the tab's menu and the palette.
/// </summary>
public sealed partial class BrowserTabViewModel : ObservableObject
{
    private readonly BrowserTabs _tabs;

    public BrowserTabViewModel(BrowserTab tab, BrowserTabs tabs)
    {
        ArgumentNullException.ThrowIfNull(tab);
        Tab = tab;
        _tabs = tabs;
    }

    public BrowserTab Tab { get; }

    public string AutomationId => "Browser.Tab." + Tab.Id;

    /// <summary>The page title, else the site, else "New tab".</summary>
    public string Title =>
        Tab.Page.Title is { Length: > 0 } title ? title
        : Tab.HasWebPage ? BrowserTabLines.Address(Tab.Page.Url!)
        : Strings.NewTab;

    public string ToolTip => Tab.HasWebPage ? Title + "\n" + Tab.Page.Url!.AbsoluteUri : Title;

    public ReadOnlyMemory<byte> Favicon => Tab.Page.Favicon;

    public bool HasFavicon => !Tab.Page.Favicon.IsEmpty;

    public bool IsLoading => Tab.Page.IsLoading;

    public bool IsPinned => Tab.IsPinned;

    public bool IsAgent => Tab.IsAgent;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public IReadOnlyList<MenuItemViewModel> Menu =>
    [
        MenuItemViewModel.ForAction(Strings.CloseTab, AutomationId + ".Menu.Close", CloseCommand),
        MenuItemViewModel.ForAction(Strings.CloseOtherTabs, AutomationId + ".Menu.CloseOthers", CloseOthersCommand),
        MenuItemViewModel.CreateSeparator(),
        MenuItemViewModel.ForAction(IsPinned ? Strings.UnpinTab : Strings.PinTab, AutomationId + ".Menu.Pin", TogglePinCommand),
    ];

    /// <summary>The page or the tab changed: every shown property is read again.</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);

    [RelayCommand]
    private Task SelectAsync() => Run(() => _tabs.Select(Tab));

    [RelayCommand]
    private Task CloseAsync() => Run(() => _tabs.Close(Tab));

    [RelayCommand]
    private Task CloseOthersAsync() => Run(() => _tabs.CloseOthers(Tab));

    [RelayCommand]
    private Task TogglePinAsync() => Run(() => _tabs.SetPinned(Tab, !Tab.IsPinned));

    private static Task Run(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
