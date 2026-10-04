using System.IO;
using System.Windows;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Instances;
using Microsoft.Extensions.Logging;

namespace CodeEditor.App.Instances;

/// <summary>
/// This window among the others (ADR 0044). Once shown it enters the window registry and keeps its folder and
/// activation time there; other launches send it requests over its pipe: come to the front, open a file. It leaves the
/// registry on exit.
/// </summary>
internal sealed partial class WindowInstance(
    WindowRegistry registry,
    InstanceServer server,
    IWorkspace workspace,
    ICommandService commands,
    TimeProvider time,
    ILogger<WindowInstance> logger) : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly DateTime _startTime = SystemProcessProbe.CurrentStartTime();
    private Window? _window;
    private WindowState _restoreState = WindowState.Normal;

    /// <summary>Opens the file the launch asked for, then joins the registry and starts serving requests.</summary>
    public async Task StartAsync(Window window, string? file)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        window.Activated += OnActivated;
        window.StateChanged += OnStateChanged;
        workspace.Changed += OnWorkspaceChanged;
        if (file is not null)
        {
            await commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(file));
        }

        Publish();
        var token = _stopping.Token;
        _ = Task.Run(() => server.RunAsync(WindowEntry.PipeNameOf(Environment.ProcessId), OnRequestAsync, token), token);
    }

    /// <summary>Leaves the registry before exit, so a restart or a new launch doesn't target this window.</summary>
    public void Leave()
    {
        _stopping.Cancel();
        workspace.Changed -= OnWorkspaceChanged;
        if (_window is { } window)
        {
            window.Activated -= OnActivated;
            window.StateChanged -= OnStateChanged;
        }

        registry.Remove(Environment.ProcessId);
    }

    public void Dispose() => _stopping.Dispose();

    private void Publish() => registry.Publish(new WindowEntry(Environment.ProcessId, _startTime, workspace.Root, time.GetUtcNow()));

    private void OnActivated(object? sender, EventArgs e) => Publish();

    private void OnWorkspaceChanged(object? sender, EventArgs e) => Publish();

    // Minimizing loses whether the window was maximized; a request restores that state.
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (_window is { WindowState: not WindowState.Minimized } window)
        {
            _restoreState = window.WindowState;
        }
    }

    private Task OnRequestAsync(InstanceRequest request) => _window!.Dispatcher.InvokeAsync(() => HandleAsync(request)).Task.Unwrap();

    private async Task HandleAsync(InstanceRequest request)
    {
        LogRequest(logger, request.Path ?? "(activate)");
        BringToFront();
        if (request.Path is { } path && File.Exists(path))
        {
            await commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(path));
        }
    }

    // Activate alone may only flash the taskbar button; the sender allowed this process to take the foreground.
    private void BringToFront()
    {
        var window = _window!;
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = _restoreState;
        }

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Request from another launch: {Path}")]
    private static partial void LogRequest(ILogger logger, string path);
}
