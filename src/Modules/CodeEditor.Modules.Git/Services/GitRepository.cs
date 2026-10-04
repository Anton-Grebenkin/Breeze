using System.Globalization;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Git.Services;

/// <summary>
/// The workspace repository for the panel and status bar: location, latest <c>git status</c> snapshot, running action
/// and last error. Refreshes never overlap: a request during a refresh repeats it once afterwards. Actions run one at a
/// time, each followed by a refresh. Calls and property changes happen on the UI thread (<see cref="GitAutoRefresh"/>
/// marshals background events there); git itself runs in the background.
/// </summary>
public sealed partial class GitRepository : ObservableObject, IDisposable
{
    private readonly GitReader _reader;
    private readonly IWorkspace _workspace;
    private readonly IContextKeyService _context;
    private readonly StatusBarViewModel _statusBar;
    private CancellationTokenSource _session = new();
    private Task _refreshing = Task.CompletedTask;
    private bool _refreshAgain;
    private bool _disposed;

    public GitRepository(GitReader reader, IWorkspace workspace, IContextKeyService context, StatusBarViewModel statusBar)
    {
        _reader = reader;
        _workspace = workspace;
        _context = context;
        _statusBar = statusBar;
        _workspace.Changed += OnWorkspaceChanged;
        Reset();
    }

    /// <summary>State was re-read; open diff tabs refresh themselves.</summary>
    public event EventHandler? StatusChanged;

    [ObservableProperty]
    public partial GitRepositoryState State { get; private set; }

    [ObservableProperty]
    public partial GitStatus Status { get; private set; } = GitStatus.Empty;

    /// <summary>Repository root and .git folder; <c>null</c> when no repository was found.</summary>
    [ObservableProperty]
    public partial GitLocation? Location { get; private set; }

    /// <summary>Running action ("Pull"); <c>null</c> when idle.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial string? Activity { get; private set; }

    /// <summary>Last git error for the panel; cleared by the next action.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    public bool IsBusy => Activity is not null;

    public bool IsReady => State == GitRepositoryState.Ready;

    /// <summary>The running or last refresh; tests await it.</summary>
    public Task Refreshing => _refreshing;

    // After disposal returns a canceled token, so continuations never touch the disposed source.
    private CancellationToken SessionToken => _disposed ? new CancellationToken(canceled: true) : _session.Token;

    /// <summary>Full path of a repository file given its root-relative path.</summary>
    public string? FullPath(string path) =>
        Location is { } location ? Path.GetFullPath(Path.Combine(location.Root, path)) : null;

    /// <summary>Root-relative path with '/'; <c>null</c> when the file is outside the repository.</summary>
    public string? RelativePath(string fullPath)
    {
        if (Location is not { } location)
        {
            return null;
        }

        var relative = Path.GetRelativePath(location.Root, fullPath);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? null : relative.Replace('\\', '/');
    }

    /// <summary>Re-reads state; during a refresh, repeats it once after the current one.</summary>
    public Task RefreshAsync()
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        if (!_refreshing.IsCompleted)
        {
            _refreshAgain = true;
            return _refreshing;
        }

        _refreshing = RefreshLoopAsync();
        return _refreshing;
    }

    /// <summary>
    /// Runs a repository action with progress and git errors shown in the panel and status bar, then re-reads state.
    /// A second action does not start while one is running.
    /// </summary>
    /// <param name="name">User-facing name: "Pull", "Commit".</param>
    /// <param name="action">Gets the repository root; <c>null</c> when there is none yet (<c>git init</c>).</param>
    /// <returns><c>true</c> when the action completed.</returns>
    public async Task<bool> RunAsync(string name, Func<string?, CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Activity is { } running)
        {
            _statusBar.Message = Format(Strings.GitBusy, running);
            return false;
        }

        var token = SessionToken;
        (Activity, Error) = (name, null);
        _statusBar.Message = Format(Strings.GitActivity, name);
        try
        {
            await action(Location?.Root, token);
            _statusBar.Message = Format(Strings.GitActivityDone, name);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (exception is GitException or IOException or UnauthorizedAccessException)
        {
            ReportError(exception.Message);
            return false;
        }
        finally
        {
            Activity = null;
            await RefreshAsync();
        }
    }

    /// <summary>Reports an error: full text in the panel, first line in the status bar.</summary>
    public void ReportError(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Error = message;
        _statusBar.Message = Format(Strings.GitErrorStatus, message.Split('\n', 2)[0].Trim());
    }

    /// <summary>Shows a non-error message in the status bar only ("No changes to commit").</summary>
    public void Notify(string message) => _statusBar.Message = message;

    public void ClearError() => Error = null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _workspace.Changed -= OnWorkspaceChanged;
        _session.Cancel();
        _session.Dispose();
    }

    partial void OnStateChanged(GitRepositoryState value) => _context.Set(GitContextKeys.State, GitContextKeys.StateValue(value));

    private async Task RefreshLoopAsync()
    {
        do
        {
            _refreshAgain = false;
            await RefreshOnceAsync(SessionToken);
        }
        while (_refreshAgain && !_disposed);
    }

    private async Task RefreshOnceAsync(CancellationToken token)
    {
        if (_workspace.Root is null)
        {
            Reset();
            return;
        }

        try
        {
            if (Location is null)
            {
                var location = await _reader.LocateAsync(token);
                token.ThrowIfCancellationRequested();
                Location = location;
            }

            await ReadStatusAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The folder changed; the next refresh reads the new one.
        }
        catch (GitException exception) when (exception.IsNotInstalled)
        {
            Apply(GitRepositoryState.NoGit, GitStatus.Empty);
        }
        catch (GitException exception)
        {
            // git refused the folder (e.g. "dubious ownership"): the panel shows the error instead of the repository.
            // If the repository was deleted or corrupted, the next refresh looks it up again.
            if (Location is null)
            {
                Apply(GitRepositoryState.NotRepository, GitStatus.Empty);
            }

            Location = null;
            Error = exception.Message;
        }
    }

    private async Task ReadStatusAsync(CancellationToken token)
    {
        if (Location is not { } location)
        {
            Apply(GitRepositoryState.NotRepository, GitStatus.Empty);
            return;
        }

        var status = await _reader.StatusAsync(location.Root, token);
        token.ThrowIfCancellationRequested();
        Apply(GitRepositoryState.Ready, status);
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Apply(GitRepositoryState state, GitStatus status)
    {
        Status = status;
        State = state;
    }

    // Another folder means another repository: running commands are canceled and state is read anew.
    private void OnWorkspaceChanged(object? sender, EventArgs e)
    {
        _session.Cancel();
        _session.Dispose();
        _session = new CancellationTokenSource();
        Reset();
    }

    private void Reset()
    {
        (Location, Error, Activity) = (null, null, null);
        Apply(_workspace.Root is null ? GitRepositoryState.NoFolder : GitRepositoryState.Unknown, GitStatus.Empty);
        _context.Set(GitContextKeys.State, GitContextKeys.StateValue(State));
    }

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
