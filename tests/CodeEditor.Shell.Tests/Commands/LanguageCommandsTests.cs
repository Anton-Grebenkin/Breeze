using CodeEditor.Core.Commands;
using CodeEditor.Core.Localization;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;

namespace CodeEditor.Shell.Tests.Commands;

/// <summary>Language choice: saves the setting, offers a restart for another language, and restarts on consent.</summary>
public sealed class LanguageCommandsTests : IDisposable
{
    private readonly ShellFixture _shell = new();
    private readonly StatusBarViewModel _statusBar = new();
    private readonly FakeSettingsService _settings = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly LanguageCommands _languages;
    private int _restarts;

    public LanguageCommandsTests()
    {
        _languages = new LanguageCommands(_settings, _statusBar, _dialogs, _shell.CommandService);
        _languages.Register(_shell.Commands, _shell.Menus);
        _shell.Commands.Register(new CommandDefinition(ShellCommandIds.Restart, "restart", (_, _) =>
        {
            _restarts++;
            return ValueTask.CompletedTask;
        }));
    }

    public void Dispose() => _languages.Dispose();

    [Fact]
    public void Submenu_ListsLanguagesInTheirOwnNames() =>
        Assert.Equal(["_Как в Windows", "_Русский", "_English"], _shell.MenuBuilder.Build(MenuIds.Language).Select(item => item.ToString()));

    [Fact]
    public async Task OtherLanguage_Confirmed_Restarts()
    {
        await _shell.CommandService.ExecuteAsync(ShellCommandIds.LanguageEnglish, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(UiLanguage.English, _settings.Written[UiLanguage.SettingKey]);
        Assert.Equal("Перезапустить Breeze, чтобы сменить язык интерфейса на «English»?", Assert.Single(_dialogs.Confirmations));
        Assert.Equal(1, _restarts);
    }

    [Fact]
    public async Task OtherLanguage_Declined_OnlySaysWhenItApplies()
    {
        _dialogs.ConfirmAnswer = false;

        await _shell.CommandService.ExecuteAsync(ShellCommandIds.LanguageEnglish, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, _restarts);
        Assert.Equal("Язык: English. Перезапустите Breeze, чтобы применить", _statusBar.Message);
    }

    [Fact]
    public async Task SameLanguage_NoRestartQuestion()
    {
        await _shell.CommandService.ExecuteAsync(ShellCommandIds.LanguageRussian, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(_dialogs.Confirmations);
        Assert.Equal("Язык: Русский", _statusBar.Message);
    }
}
