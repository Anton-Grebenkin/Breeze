using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.Theming;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;

namespace CodeEditor.Shell.Tests.Commands;

public sealed class ThemeCommandsTests : IDisposable
{
    private readonly ShellFixture _shell = new();
    private readonly FakeThemeService _themes = new();
    private readonly StatusBarViewModel _statusBar = new();
    private readonly FakeSettingsService _settings = new();
    private readonly ThemeCommands _themeCommands;

    public ThemeCommandsTests()
    {
        _themeCommands = new ThemeCommands(_themes, _settings, _statusBar);
        _themeCommands.Register(_shell.Commands, _shell.Keybindings, _shell.Menus);
    }

    [Fact]
    public void ThemeSubmenu_ListsThemesThenToggle()
    {
        var items = _shell.MenuBuilder.Build(MenuIds.Theme);

        Assert.Equal(["_Тёмная", "_Светлая", "—", "_Переключить"], items.Select(item => item.ToString()));
        Assert.Equal("Ctrl+K Ctrl+T", items[^1].InputGestureText);
    }

    public void Dispose() => _themeCommands.Dispose();

    [Fact]
    public async Task Toggle_SwitchesThemeAndReportsInStatusBar()
    {
        await Execute(ShellCommandIds.ToggleTheme);

        Assert.Equal(ThemeKind.Light, _themes.Current);
        Assert.Equal("Тема: светлая", _statusBar.Message);

        await Execute(ShellCommandIds.ToggleTheme);

        Assert.Equal(ThemeKind.Dark, _themes.Current);
    }

    [Theory]
    [InlineData(ShellCommandIds.LightTheme, ThemeKind.Light)]
    [InlineData(ShellCommandIds.DarkTheme, ThemeKind.Dark)]
    public async Task ExplicitThemeCommand_AppliesTheme(string commandId, ThemeKind expected)
    {
        await Execute(commandId);

        Assert.Equal(expected, _themes.Current);
    }


    [Fact]
    public async Task ThemeChoice_IsSavedToUserSettings()
    {
        await Execute(ShellCommandIds.LightTheme);

        Assert.Equal(ThemeNames.Light, _settings.Written[ThemeCommands.SettingKey]);
    }

    [Fact]
    public async Task BrokenSettings_ThemeStillChanges_AndErrorIsShown()
    {
        _settings.WriteError = "Не удалось записать в настройки";

        await Execute(ShellCommandIds.LightTheme);

        Assert.Equal(ThemeKind.Light, _themes.Current);
        Assert.Equal("Не удалось записать в настройки", _statusBar.Message);
    }
    [Fact]
    public void Toggle_IsBoundToChord()
    {
        var binding = _shell.Keybindings.FindForCommand(ShellCommandIds.ToggleTheme);

        Assert.Equal(KeySequence.Parse("Ctrl+K Ctrl+T"), binding?.Sequence);
    }

    [Fact]
    public void Dispose_RemovesCommandsAndKeybindings()
    {
        _themeCommands.Dispose();

        Assert.Empty(_shell.Commands.Commands);
        Assert.Null(_shell.Keybindings.FindForCommand(ShellCommandIds.ToggleTheme));
        Assert.Empty(_shell.Menus.GetItems(MenuIds.Theme));
    }

    private async Task Execute(string commandId)
    {
        var status = await _shell.CommandService.ExecuteAsync(commandId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandExecutionStatus.Succeeded, status);
    }
}
