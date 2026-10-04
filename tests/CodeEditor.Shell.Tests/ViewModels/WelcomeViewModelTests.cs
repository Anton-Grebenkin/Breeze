using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Tests.ViewModels;

public sealed class WelcomeViewModelTests
{
    private readonly ShellFixture _shell = new();

    [Fact]
    public void Shortcuts_EmptyWhenNothingRegistered()
    {
        using var welcome = CreateWelcome();

        Assert.Empty(welcome.Shortcuts);
    }

    [Fact]
    public void Shortcuts_ShowOnlyCommandsWithKeybindings()
    {
        using var welcome = CreateWelcome();

        _shell.RegisterCommand(ShellCommandIds.ShowCommands, "Показать все команды");
        _shell.RegisterCommand(ShellCommandIds.ToggleTheme, "Переключить тему");
        _shell.Bind("Ctrl+K Ctrl+T", ShellCommandIds.ToggleTheme);

        Assert.Equal([new ShortcutItem(ShellCommandIds.ToggleTheme, "Переключить тему", "Ctrl+K Ctrl+T")], welcome.Shortcuts);
    }

    [Fact]
    public void Shortcuts_KeepFeaturedOrder()
    {
        _shell.RegisterCommand(ShellCommandIds.ToggleTheme, "Переключить тему");
        _shell.RegisterCommand(ShellCommandIds.ShowCommands, "Показать все команды");
        _shell.Bind("Ctrl+K Ctrl+T", ShellCommandIds.ToggleTheme);
        _shell.Bind("Ctrl+Shift+P", ShellCommandIds.ShowCommands);

        using var welcome = CreateWelcome();

        Assert.Equal(["Показать все команды", "Переключить тему"], welcome.Shortcuts.Select(item => item.Title));
    }

    [Fact]
    public async Task ExecuteCommand_RunsClickedShortcut()
    {
        var executed = false;
        _shell.Commands.Register(new(ShellCommandIds.ToggleTheme, "Переключить тему", (_, _) =>
        {
            executed = true;
            return ValueTask.CompletedTask;
        }));
        _shell.Bind("Ctrl+K Ctrl+T", ShellCommandIds.ToggleTheme);
        using var welcome = CreateWelcome();

        await welcome.ExecuteCommand.ExecuteAsync(welcome.Shortcuts[0]);

        Assert.True(executed);
    }

    [Fact]
    public void Dispose_StopsTrackingRegistries()
    {
        var welcome = CreateWelcome();
        welcome.Dispose();

        _shell.RegisterCommand(ShellCommandIds.ToggleTheme, "Переключить тему");
        _shell.Bind("Ctrl+K Ctrl+T", ShellCommandIds.ToggleTheme);

        Assert.Empty(welcome.Shortcuts);
    }

    private WelcomeViewModel CreateWelcome() => new(_shell.Commands, _shell.Keybindings, _shell.CommandService, _shell.RecentFolders);
}
