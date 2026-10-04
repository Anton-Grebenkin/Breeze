using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Modules.Updates.Resources;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Updates.Services;

/// <summary>
/// Updates as in VS Code: shortly after startup the installed app looks for a newer GitHub release and downloads it in
/// the background; a status bar item then offers a restart, otherwise Velopack installs it at the next start.
/// "Check for Updates…" does the same on demand and reports every outcome; background failures only go to the log.
/// With several windows open only the first one checks, and installing waits until the others are closed: the updater
/// replaces the files they run from (ADR 0044).
/// </summary>
public sealed partial class UpdateService : IDisposable
{
    public const string StatusItemId = "update";

    /// <summary>The background check waits for startup to finish: opening the folder and tabs comes first.</summary>
    public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

    // Rightmost, after the editor items, as VS Code shows updates on the far side.
    private const int StatusOrder = 100;

    private readonly IAppUpdater _updater;
    private readonly IOptionsMonitor<UpdateSettings> _settings;
    private readonly ProductInfo _product;
    private readonly StatusBarViewModel _statusBar;
    private readonly IDialogService _dialogs;
    private readonly AppRestart _restart;
    private readonly ICommandService _commands;
    private readonly IAppWindows _windows;
    private readonly TimeProvider _time;
    private readonly ILogger<UpdateService> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly StatusBarItemViewModel _item = new(StatusItemId, StatusOrder) { IsVisible = false };
    private IDisposable? _registration;
    private string? _readyVersion;

    public UpdateService(
        IAppUpdater updater,
        IOptionsMonitor<UpdateSettings> settings,
        ProductInfo product,
        StatusBarViewModel statusBar,
        IDialogService dialogs,
        AppRestart restart,
        ICommandService commands,
        IAppWindows windows,
        TimeProvider time,
        ILogger<UpdateService> logger)
    {
        _updater = updater;
        _settings = settings;
        _product = product;
        _statusBar = statusBar;
        _dialogs = dialogs;
        _restart = restart;
        _commands = commands;
        _windows = windows;
        _time = time;
        _logger = logger;
        _item.Command = new AsyncRelayCommand(RestartToUpdateAsync);
    }

    public UpdateState State { get; private set; }

    /// <summary>The status bar item: download progress, then the update ready to install.</summary>
    public StatusBarItemViewModel Item => _item;

    /// <summary>The check started by <see cref="Start"/>; tests wait for it.</summary>
    internal Task BackgroundCheck { get; private set; } = Task.CompletedTask;

    public void Start()
    {
        _registration = _statusBar.Add(_item);
        if (_updater.IsInstalled && _settings.CurrentValue.Mode == UpdateMode.Default && _windows.CountOthers() == 0)
        {
            BackgroundCheck = CheckAfterStartupAsync();
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
        _registration?.Dispose();
    }

    /// <summary>
    /// Looks for an update and downloads it. <paramref name="interactive"/> — the user asked: every outcome is
    /// reported, and a downloaded update offers a restart.
    /// </summary>
    public async Task CheckAsync(bool interactive)
    {
        if (!_updater.IsInstalled)
        {
            Report(interactive, Strings.NotInstalled);
            return;
        }

        switch (State)
        {
            case UpdateState.Ready:
                if (interactive)
                {
                    await OfferRestartAsync();
                }

                return;
            case UpdateState.Checking or UpdateState.Downloading:
                Report(interactive, Strings.CheckInProgress);
                return;
        }

        if (await FindAsync(interactive) is { } version && await DownloadAsync(version, interactive) && interactive)
        {
            await OfferRestartAsync();
        }
    }

    /// <summary>Restarts through the usual window close (unsaved files, session) and installs the downloaded update.</summary>
    public async Task RestartToUpdateAsync()
    {
        if (State != UpdateState.Ready)
        {
            _statusBar.Message = Strings.NothingToApply;
            return;
        }

        if (_windows.CountOthers() is var others and > 0)
        {
            _statusBar.Message = Format(Strings.CloseOtherWindows, others);
            return;
        }

        _restart.RelaunchWith(_updater.ApplyAfterExit);
        await _commands.ExecuteAsync(ShellCommandIds.Restart);
    }

    private async Task CheckAfterStartupAsync()
    {
        try
        {
            await Task.Delay(StartupDelay, _time, _stopping.Token);
            await CheckAsync(interactive: false);
        }
        catch (OperationCanceledException)
        {
            // The app is closing.
        }
    }

    private async Task<string?> FindAsync(bool interactive)
    {
        State = UpdateState.Checking;
        Report(interactive, Strings.Checking);
        try
        {
            var version = await _updater.FindUpdateAsync(_stopping.Token);
            Report(interactive && version is null, Format(Strings.UpToDate, _product.Version));
            return version;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCheckFailed(_logger, exception);
            Report(interactive, Format(Strings.CheckFailed, exception.Message));
            return null;
        }
        finally
        {
            State = UpdateState.Idle;
        }
    }

    private async Task<bool> DownloadAsync(string version, bool interactive)
    {
        LogFound(_logger, version, _product.Version);
        State = UpdateState.Downloading;
        ShowProgress(version, percent: 0);
        try
        {
            await _updater.DownloadAsync(new Progress<int>(percent => ShowProgress(version, percent)), _stopping.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogDownloadFailed(_logger, version, exception);
            State = UpdateState.Idle;
            _item.IsVisible = false;
            Report(interactive, Format(Strings.DownloadFailed, version, exception.Message));
            return false;
        }

        State = UpdateState.Ready;
        _readyVersion = version;
        _item.Text = Format(Strings.ReadyItem, version);
        _item.ToolTip = Strings.ReadyToolTip;
        _statusBar.Message = Format(Strings.Ready, version);
        LogReady(_logger, version);
        return true;
    }

    private async Task OfferRestartAsync()
    {
        if (_dialogs.Confirm(Format(Strings.RestartPrompt, _readyVersion ?? string.Empty), Strings.RestartDetail, Strings.RestartConfirm))
        {
            await RestartToUpdateAsync();
        }
    }

    // Progress arrives on the UI thread: Progress<T> posts to the context it was created in.
    private void ShowProgress(string version, int percent)
    {
        if (State != UpdateState.Downloading)
        {
            return;
        }

        _item.Text = string.Format(CultureInfo.CurrentCulture, Strings.Downloading, version, percent);
        _item.ToolTip = null;
        _item.IsVisible = true;
    }

    private void Report(bool show, string message)
    {
        if (show)
        {
            _statusBar.Message = message;
        }
    }

    private static string Format(string template, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, template, arguments);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed")]
    private static partial void LogCheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update {Version} found, current version {Current}")]
    private static partial void LogFound(ILogger logger, string version, string current);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update {Version} download failed")]
    private static partial void LogDownloadFailed(ILogger logger, string version, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update {Version} downloaded, installs on restart")]
    private static partial void LogReady(ILogger logger, string version);
}
