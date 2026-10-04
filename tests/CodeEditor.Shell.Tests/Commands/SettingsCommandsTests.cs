using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Commands;

public sealed class SettingsCommandsTests : IDisposable
{
    private readonly ShellFixture _shell = new();
    private readonly FakeSettingsService _settings = new() { WorkspaceSettingsPath = @"C:\repo\.breeze\settings.json" };
    private readonly StatusBarViewModel _statusBar = new();
    private readonly List<object?> _opened = [];
    private readonly UserKeybindings _userKeybindings;
    private readonly SettingsCommands _commands;

    public SettingsCommandsTests()
    {
        _shell.Commands.Register(new CommandDefinition(ShellCommandIds.OpenFile, "Открыть файл", (argument, _) =>
        {
            _opened.Add(argument);
            return ValueTask.CompletedTask;
        }));
        _userKeybindings = new UserKeybindings(_shell.Keybindings, _shell.FileSystem, _shell.Paths, new InlineUiDispatcher(), NullLogger<UserKeybindings>.Instance);
        _commands = new SettingsCommands(_settings, _userKeybindings, _shell.CommandService, _statusBar);
        _commands.Register(_shell.Commands, _shell.Keybindings, _shell.Menus);
    }

    public void Dispose()
    {
        _commands.Dispose();
        _shell.Dispose();
    }

    [Fact]
    public async Task OpenUserSettings_OpensFile_AndIsBoundToCtrlComma()
    {
        await _shell.CommandService.ExecuteAsync(SettingsCommands.OpenUserSettingsId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new OpenFileRequest(_settings.UserSettingsPath), Assert.Single(_opened));
        Assert.Equal(KeySequence.Parse("Ctrl+,"), _shell.Keybindings.FindForCommand(SettingsCommands.OpenUserSettingsId)?.Sequence);
    }

    [Fact]
    public async Task OpenWorkspaceSettings_NeedsOpenFolder()
    {
        Assert.Equal(CommandExecutionStatus.Disabled,
            await _shell.CommandService.ExecuteAsync(SettingsCommands.OpenWorkspaceSettingsId, cancellationToken: TestContext.Current.CancellationToken));

        _shell.Context.Set(IWorkspace.OpenContextKey, true);
        await _shell.CommandService.ExecuteAsync(SettingsCommands.OpenWorkspaceSettingsId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new OpenFileRequest(_settings.WorkspaceSettingsPath!), Assert.Single(_opened));
    }


    [Fact]
    public async Task OpenKeybindings_CreatesTemplateAndOpens()
    {
        await _shell.CommandService.ExecuteAsync(SettingsCommands.OpenKeybindingsId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new OpenFileRequest(_userKeybindings.FilePath), Assert.Single(_opened));
        Assert.True(_shell.FileSystem.FileExists(_userKeybindings.FilePath));
    }

    [Fact]
    public void KeybindingErrors_ShowInStatusBar()
    {
        _shell.FileSystem.AddFile(_userKeybindings.FilePath, "[ { \"key\": \"ctrl+nope\", \"command\": \"x\" } ]");

        _userKeybindings.Reload();

        Assert.StartsWith("keybindings.json: неизвестное сочетание", _statusBar.Message, StringComparison.Ordinal);
    }
    [Fact]
    public void BrokenSettings_ShowErrorInStatusBar()
    {
        _settings.Error = "Ошибка в settings.json";

        _settings.TrySetUserValue("a", 1, out _);

        Assert.Equal("Ошибка в settings.json", _statusBar.Message);
    }
}
