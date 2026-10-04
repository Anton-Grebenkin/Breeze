using CodeEditor.Core.Commands;
using CodeEditor.Core.Logging;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Shell.Tests.Commands;

public sealed class LogCommandsTests : IDisposable
{
    private readonly ShellFixture _shell = new();
    private readonly FakeLogFiles _files = new();
    private readonly FakeSettingsService _settings = new();
    private readonly FakeQuickPick _quickPick = new();
    private readonly FakeSystemShell _system = new();
    private readonly StatusBarViewModel _statusBar = new();
    private readonly List<object?> _opened = [];
    private readonly LogCommands _commands;

    public LogCommandsTests()
    {
        _shell.Commands.Register(new CommandDefinition(ShellCommandIds.OpenFile, "Открыть файл", (argument, _) =>
        {
            _opened.Add(argument);
            return ValueTask.CompletedTask;
        }));
        _commands = new LogCommands(_files, new LogLevelSwitch(LogLevel.Debug), _shell.CommandService, _quickPick, _settings, _system, _statusBar);
        _commands.Register(_shell.Commands);
    }

    public void Dispose()
    {
        _commands.Dispose();
        _shell.Dispose();
    }

    [Fact]
    public async Task OpenLog_OpensCurrentFileInTab()
    {
        await Execute(LogCommands.OpenLogId);

        Assert.Equal(new OpenFileRequest(_files.CurrentFile!), Assert.Single(_opened));
    }

    [Fact]
    public async Task OpenLog_WithoutFile_ExplainsInStatusBar()
    {
        _files.CurrentFile = null;

        await Execute(LogCommands.OpenLogId);

        Assert.Empty(_opened);
        Assert.Contains(_files.Folder, _statusBar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevealLogs_SelectsCurrentFileInExplorer()
    {
        await Execute(LogCommands.RevealLogsId);

        Assert.Equal(_files.CurrentFile, _system.Revealed);
    }

    [Fact]
    public async Task SetLogLevel_MarksCurrent_AndWritesSetting()
    {
        await Execute(LogCommands.SetLogLevelId);

        Assert.StartsWith("текущий", Assert.Single(_quickPick.Items, item => item.Title == "Отладка").Detail, StringComparison.Ordinal);

        await _quickPick.PickAsync("Предупреждения");

        Assert.Equal("warning", _settings.Written[LoggingOptions.LevelKey]);
        Assert.Equal("Уровень журнала: Предупреждения", _statusBar.Message);
    }

    private async Task Execute(string commandId) =>
        Assert.Equal(CommandExecutionStatus.Succeeded, await _shell.CommandService.ExecuteAsync(commandId, cancellationToken: TestContext.Current.CancellationToken));

    private sealed class FakeLogFiles : ILogFiles
    {
        public string Folder => @"C:\data\logs";

        public string? CurrentFile { get; set; } = @"C:\data\logs\codeeditor-20260927.log";
    }
}
