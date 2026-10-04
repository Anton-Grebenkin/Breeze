using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Logging;
using CodeEditor.Core.Settings;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Shell.Commands;

/// <summary>
/// Log commands, like "Developer: Open Log File…" and "Set Log Level…" in VS Code: open the current log in a tab,
/// reveal the log folder in Windows Explorer, pick the level (<c>log.level</c>).
/// </summary>
public sealed class LogCommands(
    ILogFiles logFiles,
    LogLevelSwitch levels,
    ICommandService commandService,
    IQuickPick quickPick,
    ISettingsService settings,
    ISystemShell shell,
    StatusBarViewModel statusBar) : IDisposable
{
    public const string OpenLogId = "developer.openLog";
    public const string RevealLogsId = "developer.revealLogs";
    public const string SetLogLevelId = "developer.setLogLevel";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var category = Strings.CategoryDeveloper;
        _registrations.Add(commands.Register(new CommandDefinition(OpenLogId, Strings.OpenLog, (_, _) => OpenLogAsync(), category)));
        _registrations.Add(commands.Register(new CommandDefinition(RevealLogsId, Strings.RevealLogs, (_, _) =>
        {
            shell.RevealInFileManager(logFiles.CurrentFile ?? logFiles.Folder);
            return ValueTask.CompletedTask;
        }, category)));
        _registrations.Add(commands.Register(new CommandDefinition(SetLogLevelId, Strings.SetLogLevel, (_, _) =>
        {
            PickLevel();
            return ValueTask.CompletedTask;
        }, category)));
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private async ValueTask OpenLogAsync()
    {
        if (logFiles.CurrentFile is not { } path)
        {
            statusBar.Message = Format(Strings.LogUnavailable, logFiles.Folder);
            return;
        }

        await commandService.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(path));
    }

    private void PickLevel()
    {
        var items = Levels()
            .Select(level => new QuickPickItem(level.Level.ToString(), level.Title)
            {
                Detail = level.Level == levels.Minimum ? Format(Strings.LogLevelCurrent, level.Detail) : level.Detail,
            })
            .ToList();
        quickPick.Show(new QuickPickProvider(Strings.LogLevelPickTitle, items, item =>
        {
            var value = JsonNamingPolicy.CamelCase.ConvertName(item.Id);
            statusBar.Message = settings.TrySetUserValue(LoggingOptions.LevelKey, value, out var error) ? Format(Strings.LogLevelChanged, item.Title) : error;
            return Task.CompletedTask;
        }));
    }

    private static (LogLevel Level, string Title, string Detail)[] Levels() =>
    [
        (LogLevel.Trace, Strings.LogLevelTrace, Strings.LogLevelTraceDetail),
        (LogLevel.Debug, Strings.LogLevelDebug, Strings.LogLevelDebugDetail),
        (LogLevel.Information, Strings.LogLevelInformation, Strings.LogLevelInformationDetail),
        (LogLevel.Warning, Strings.LogLevelWarning, Strings.LogLevelWarningDetail),
        (LogLevel.Error, Strings.LogLevelError, Strings.LogLevelErrorDetail),
    ];

    private static string Format(string template, string argument) =>
        string.Format(CultureInfo.CurrentCulture, template, argument);
}
