using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Modules.Updates.Services;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Updates.Tests;

public sealed class UpdateServiceTests : IDisposable
{
    private readonly FakeUpdater _updater = new();
    private readonly TestOptionsMonitor<UpdateSettings> _settings = new(new UpdateSettings());
    private readonly StatusBarViewModel _statusBar = new();
    private readonly FakeDialogs _dialogs = new() { ConfirmAnswer = false };
    private readonly AppRestart _restart = new();
    private readonly CommandRegistry _commands = new();
    private readonly ManualTimeProvider _time = new();
    private readonly FakeAppWindows _windows = new();
    private readonly UpdateService _service;
    private int _restarts;

    public UpdateServiceTests()
    {
        _commands.Register(new CommandDefinition(ShellCommandIds.Restart, "Перезапустить", (_, _) =>
        {
            _restarts++;
            _restart.Request();
            return ValueTask.CompletedTask;
        }));
        var commandService = new CommandService(_commands, new ContextKeyService(), NullLogger<CommandService>.Instance);
        var product = new ProductInfo("Breeze", "0.1.0-alpha.1", "abcdef1234", new Uri("https://github.com/owner/breeze"));
        _service = new UpdateService(_updater, _settings, product, _statusBar, _dialogs, _restart, commandService, _windows, _time, NullLogger<UpdateService>.Instance);
    }

    public void Dispose() => _service.Dispose();

    [Fact]
    public async Task Start_InstalledApp_ChecksAfterStartupAndDownloadsInBackground()
    {
        _updater.Release = "0.1.0-alpha.2";
        _service.Start();
        Assert.Equal(0, _updater.Checks);

        _time.Advance(UpdateService.StartupDelay);
        await _service.BackgroundCheck;

        Assert.Equal(UpdateState.Ready, _service.State);
        Assert.Contains(_service.Item, _statusBar.Items);
        Assert.True(_service.Item.IsVisible);
        Assert.Equal("Обновление 0.1.0-alpha.2", _service.Item.Text);
        Assert.Contains("0.1.0-alpha.2", _statusBar.Message, StringComparison.Ordinal);
        Assert.Empty(_dialogs.Confirmations);
    }

    [Fact]
    public async Task Start_ManualMode_DoesNotCheck()
    {
        _settings.Set(new UpdateSettings { Mode = UpdateMode.Manual });
        _service.Start();

        _time.Advance(UpdateService.StartupDelay);
        await _service.BackgroundCheck;

        Assert.Equal(0, _updater.Checks);
    }

    [Fact]
    public async Task Start_NotInstalled_DoesNotCheck()
    {
        _updater.IsInstalled = false;
        _service.Start();

        _time.Advance(UpdateService.StartupDelay);
        await _service.BackgroundCheck;

        Assert.Equal(0, _updater.Checks);
        Assert.False(_service.Item.IsVisible);
    }

    [Fact]
    public async Task Check_NotInstalled_ExplainsWhereUpdatesWork()
    {
        _updater.IsInstalled = false;

        await _service.CheckAsync(interactive: true);

        Assert.Equal(0, _updater.Checks);
        Assert.Contains("Setup.exe", _statusBar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_LatestVersion_SaysSo()
    {
        await _service.CheckAsync(interactive: true);

        Assert.Equal(UpdateState.Idle, _service.State);
        Assert.Equal(0, _updater.Downloads);
        Assert.Equal("Установлена последняя версия Breeze — 0.1.0-alpha.1.", _statusBar.Message);
    }

    [Fact]
    public async Task Check_Failure_ReportedToUserAndCanRetry()
    {
        _updater.FindError = new HttpRequestException("нет сети");

        await _service.CheckAsync(interactive: true);

        Assert.Equal(UpdateState.Idle, _service.State);
        Assert.Equal("Не удалось проверить обновления: нет сети", _statusBar.Message);
    }

    [Fact]
    public async Task BackgroundFailure_StaysQuiet()
    {
        _updater.FindError = new HttpRequestException("нет сети");
        _service.Start();

        _time.Advance(UpdateService.StartupDelay);
        await _service.BackgroundCheck;

        Assert.Equal(StatusBarViewModel.ReadyMessage, _statusBar.Message);
    }

    [Fact]
    public async Task DownloadFailure_HidesItemAndReports()
    {
        _updater.Release = "0.1.0-alpha.2";
        _updater.DownloadError = new IOException("диск заполнен");

        await _service.CheckAsync(interactive: true);

        Assert.Equal(UpdateState.Idle, _service.State);
        Assert.False(_service.Item.IsVisible);
        Assert.Equal("Не удалось загрузить обновление 0.1.0-alpha.2: диск заполнен", _statusBar.Message);
    }

    [Fact]
    public async Task Check_FoundAndConfirmed_RestartsThroughTheUpdater()
    {
        _updater.Release = "0.1.0-alpha.2";
        _dialogs.ConfirmAnswer = true;

        await _service.CheckAsync(interactive: true);

        Assert.Single(_dialogs.Confirmations);
        Assert.Equal(1, _restarts);
        Assert.True(_restart.IsRequested);
        _restart.Relaunch!();
        Assert.Equal(1, _updater.Applied);
    }

    [Fact]
    public async Task Check_FoundAndPostponed_KeepsItemAndDoesNotDownloadTwice()
    {
        _updater.Release = "0.1.0-alpha.2";

        await _service.CheckAsync(interactive: true);
        await _service.CheckAsync(interactive: true);

        Assert.Equal(0, _restarts);
        Assert.Equal(1, _updater.Downloads);
        Assert.Equal(2, _dialogs.Confirmations.Count);
        Assert.True(_service.Item.IsVisible);
        Assert.Null(_restart.Relaunch);
    }

    [Fact]
    public async Task RestartToUpdate_WithoutDownload_ExplainsInsteadOfRestarting()
    {
        await _service.RestartToUpdateAsync();

        Assert.Equal(0, _restarts);
        Assert.Contains("ещё не загружено", _statusBar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusItemClick_RestartsToUpdate()
    {
        _updater.Release = "0.1.0-alpha.2";
        await _service.CheckAsync(interactive: false);

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)_service.Item.Command!).ExecuteAsync(null);

        Assert.Equal(1, _restarts);
        Assert.NotNull(_restart.Relaunch);
    }

    // The updater replaces the files every window runs from: one window checks, installing waits for the others.
    [Fact]
    public async Task OtherWindowsOpen_NoBackgroundCheck()
    {
        _windows.EmptyWindows = 1;
        _service.Start();

        _time.Advance(UpdateService.StartupDelay);
        await _service.BackgroundCheck;

        Assert.Equal(0, _updater.Checks);
    }

    [Fact]
    public async Task RestartToUpdate_WithOtherWindows_AsksToCloseThem()
    {
        _updater.Release = "0.1.0-alpha.2";
        await _service.CheckAsync(interactive: false);
        _windows.EmptyWindows = 2;

        await _service.RestartToUpdateAsync();

        Assert.Equal(0, _restarts);
        Assert.Null(_restart.Relaunch);
        Assert.StartsWith("Закройте другие окна Breeze (2)", _statusBar.Message, StringComparison.Ordinal);
    }
}
