using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Shell.Menus;

/// <summary>
/// A whole menu (menu bar or context menu). Rebuilt when menus, commands or keybindings change.
/// </summary>
public sealed partial class MenuViewModel : ObservableObject, IDisposable
{
    private readonly string _menuId;
    private readonly MenuBuilder _builder;
    private readonly IMenuRegistry _menus;
    private readonly ICommandRegistry _commands;
    private readonly IKeybindingRegistry _keybindings;

    public MenuViewModel(
        string menuId,
        MenuBuilder builder,
        IMenuRegistry menus,
        ICommandRegistry commands,
        IKeybindingRegistry keybindings)
    {
        _menuId = menuId;
        _builder = builder;
        _menus = menus;
        _commands = commands;
        _keybindings = keybindings;

        _menus.Changed += OnRegistryChanged;
        _commands.Changed += OnRegistryChanged;
        _keybindings.Changed += OnRegistryChanged;
        Rebuild();
    }

    [ObservableProperty]
    public partial IReadOnlyList<MenuItemViewModel> Items { get; private set; } = [];

    /// <summary>Refreshes visibility and availability of top-level items before the menu opens.</summary>
    public void Refresh()
    {
        foreach (var item in Items)
        {
            item.RefreshState();
        }

        MenuSeparators.Update(Items);
    }

    public void Dispose()
    {
        _menus.Changed -= OnRegistryChanged;
        _commands.Changed -= OnRegistryChanged;
        _keybindings.Changed -= OnRegistryChanged;
    }

    private void OnRegistryChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild() => Items = _builder.Build(_menuId);
}
