using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Resources;

namespace CodeEditor.Shell.Commands;

/// <summary>
/// Shell menu skeleton and palette commands: commands (<c>Ctrl+Shift+P</c>, <c>F1</c>), files (<c>Ctrl+P</c>),
/// line (<c>Ctrl+G</c>). Modules add their items to the same menus.
/// </summary>
public sealed class WorkbenchContributions(CommandPaletteViewModel palette) : IDisposable
{
    private const string PaletteGroup = "1_palette";
    private const string AppearanceGroup = "2_appearance";
    private const string GoGroup = "1_go";

    // The last menu: modules add theirs before it (Terminal is 5).
    private const int HelpMenuOrder = 9;

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        AddPaletteCommand(commands, ShellCommandIds.ShowCommands, Strings.ShowAllCommands, CommandsQuickOpenProvider.CommandsPrefix);
        AddPaletteCommand(commands, ShellCommandIds.QuickOpen, Strings.GoToFile, string.Empty);
        AddPaletteCommand(commands, ShellCommandIds.GoToLine, Strings.GoToLine, GoToLineQuickOpenProvider.GoToLinePrefix);

        Bind(keybindings, "F1", ShellCommandIds.ShowCommands);
        Bind(keybindings, "Ctrl+Shift+P", ShellCommandIds.ShowCommands);
        Bind(keybindings, "Ctrl+P", ShellCommandIds.QuickOpen);
        Bind(keybindings, "Ctrl+G", ShellCommandIds.GoToLine);

        RegisterMenuBar(menus);
        RegisterViewMenu(menus);
        RegisterGoMenu(menus);
        RegisterManageMenu(menus);
    }

    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
    }

    // Order as in VS Code and Visual Studio. Empty menus are hidden.
    private void RegisterMenuBar(IMenuRegistry menus)
    {
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.MenuBar, MenuIds.File, Strings.FileMenu, order: 1)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.MenuBar, MenuIds.Edit, Strings.EditMenu, order: 2)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.MenuBar, MenuIds.View, Strings.ViewMenu, order: 3)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.MenuBar, MenuIds.Go, Strings.GoMenu, order: 4)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.MenuBar, MenuIds.Help, Strings.HelpMenu, order: HelpMenuOrder)));
    }

    private void RegisterViewMenu(IMenuRegistry menus)
    {
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(
            MenuIds.View, ShellCommandIds.ShowCommands, PaletteGroup, title: Strings.CommandPaletteMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(
            MenuIds.View, MenuIds.Theme, Strings.ThemeMenu, AppearanceGroup)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(
            MenuIds.View, MenuIds.Language, Strings.LanguageMenu, AppearanceGroup, order: 1)));
    }

    private void RegisterGoMenu(IMenuRegistry menus)
    {
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(
            MenuIds.Go, ShellCommandIds.QuickOpen, GoGroup, order: 1, title: Strings.GoToFileMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(
            MenuIds.Go, ShellCommandIds.GoToLine, GoGroup, order: 2, title: Strings.GoToLineMenu)));
    }

    private void RegisterManageMenu(IMenuRegistry menus)
    {
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(
            MenuIds.Manage, ShellCommandIds.ShowCommands, PaletteGroup, title: Strings.CommandPaletteMenuManage)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(
            MenuIds.Manage, MenuIds.Theme, Strings.ThemeMenuManage, AppearanceGroup)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(
            MenuIds.Manage, MenuIds.Language, Strings.LanguageMenuManage, AppearanceGroup, order: 1)));
    }

    private void AddPaletteCommand(ICommandRegistry commands, string id, string title, string prefix) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, (_, _) =>
        {
            palette.Open(prefix);
            return ValueTask.CompletedTask;
        })));

    private void Bind(IKeybindingRegistry keybindings, string keys, string commandId) =>
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId)));
}
