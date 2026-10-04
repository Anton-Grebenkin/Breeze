using CodeEditor.Core.Commands;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Tests.ViewModels;

public sealed class TitleBarViewModelTests
{
    private readonly ShellFixture _shell = new();

    [Fact]
    public void SearchShortcut_FollowsKeybindingRegistry()
    {
        using var titleBar = CreateTitleBar();
        Assert.Null(titleBar.SearchShortcut);

        var registration = _shell.Bind("Ctrl+P", ShellCommandIds.QuickOpen);

        Assert.Equal("Ctrl+P", titleBar.SearchShortcut);

        registration.Dispose();

        Assert.Null(titleBar.SearchShortcut);
    }

    [Fact]
    public async Task QuickOpenCommand_ExecutesQuickOpen()
    {
        var executed = false;
        _shell.Commands.Register(new CommandDefinition(ShellCommandIds.QuickOpen, "Перейти к файлу…", (_, _) =>
        {
            executed = true;
            return ValueTask.CompletedTask;
        }));
        using var titleBar = CreateTitleBar();

        await titleBar.QuickOpenCommand.ExecuteAsync(null);

        Assert.True(executed);
    }

    [Fact]
    public void MenuBar_ShowsNonEmptyTopLevelMenus()
    {
        _shell.RegisterCommand("view.zoom", "Масштаб");
        _shell.Menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.MenuBar, MenuIds.File, "_Файл", order: 1));
        _shell.Menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.MenuBar, MenuIds.View, "_Вид", order: 2));
        _shell.Menus.Register(MenuItemDefinition.ForCommand(MenuIds.View, "view.zoom"));

        using var titleBar = CreateTitleBar();

        Assert.Equal(["_Вид"], titleBar.MenuBar.Items.Select(item => item.Header));
    }

    private TitleBarViewModel CreateTitleBar() => new(_shell.CommandService, _shell.Keybindings, _shell.Workspace, _shell.MenuFactory);
}
