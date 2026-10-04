using CodeEditor.Core.Context;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.Menus;

/// <summary>
/// Menu item: a command, a submenu or a separator. Visibility and enabled state are refreshed when the parent opens.
/// </summary>
public sealed partial class MenuItemViewModel : ObservableObject
{
    private readonly ContextExpression? _when;
    private readonly IContextKeyLookup? _context;

    private MenuItemViewModel(
        string header,
        string automationId,
        IReadOnlyList<MenuItemViewModel> items,
        ContextExpression? when = null,
        IContextKeyLookup? context = null)
    {
        Header = header;
        AutomationId = automationId;
        Items = items;
        _when = when;
        _context = context;
    }

    /// <summary>Header; "_" before a letter makes it the access key (<c>Alt</c>+letter).</summary>
    public string Header { get; }

    public string AutomationId { get; }

    /// <summary>The command's keybinding, shown right of the header.</summary>
    public string? InputGestureText { get; private init; }

    public IAsyncRelayCommand? Command { get; private init; }

    public IReadOnlyList<MenuItemViewModel> Items { get; }

    public bool IsSeparator { get; private init; }

    /// <summary>The selected option in a group (e.g. model parameters), shown with a check mark.</summary>
    public bool IsChecked { get; private init; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>Whether the submenu is open; bound to <c>MenuItem.IsSubmenuOpen</c>.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    public static MenuItemViewModel ForCommand(
        string header,
        string commandId,
        string? inputGestureText,
        IAsyncRelayCommand command,
        ContextExpression? when,
        IContextKeyLookup context) =>
        new(header, $"Menu.{commandId}", [], when, context)
        {
            InputGestureText = inputGestureText,
            Command = command,
        };

    public static MenuItemViewModel ForSubmenu(
        string header,
        string submenuId,
        IReadOnlyList<MenuItemViewModel> items,
        ContextExpression? when,
        IContextKeyLookup context) =>
        new(header, $"Menu.{submenuId}", items, when, context);

    /// <summary>A tool window menu item outside the menu registry: an action or a checkable option.</summary>
    public static MenuItemViewModel ForAction(string header, string automationId, IAsyncRelayCommand command, bool isChecked = false) =>
        new(header, automationId, []) { Command = command, IsChecked = isChecked };

    /// <summary>A tool window submenu outside the menu registry, e.g. the options of one parameter.</summary>
    public static MenuItemViewModel ForGroup(string header, string automationId, IReadOnlyList<MenuItemViewModel> items) =>
        new(header, automationId, items);

    public static MenuItemViewModel CreateSeparator() => new(string.Empty, string.Empty, []) { IsSeparator = true };

    /// <summary>Re-evaluates the children's <c>when</c> visibility and command availability.</summary>
    public void RefreshChildren()
    {
        foreach (var child in Items)
        {
            child.RefreshState();
        }

        MenuSeparators.Update(Items);
    }

    internal void RefreshState()
    {
        IsVisible = _when is null || _context is null || _when.Evaluate(_context);
        Command?.NotifyCanExecuteChanged();
    }

    partial void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            RefreshChildren();
        }
    }

    public override string ToString() => IsSeparator ? "—" : Header;
}
