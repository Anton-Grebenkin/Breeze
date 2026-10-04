using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;

namespace CodeEditor.Shell.Menus;

/// <summary>
/// Creates a <see cref="MenuViewModel"/> by menu id: the menu bar, Manage, module context menus.
/// </summary>
public sealed class MenuViewModelFactory(
    MenuBuilder builder,
    IMenuRegistry menus,
    ICommandRegistry commands,
    IKeybindingRegistry keybindings)
{
    public MenuViewModel Create(string menuId) => new(menuId, builder, menus, commands, keybindings);
}
