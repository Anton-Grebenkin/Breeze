using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Viewers.Commands;
using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>
/// A file viewer in an editor tab (ADR 0037). Creation reads nothing: the file is read in the background when the tab is
/// first shown, so a restored session with a dozen images spends no time on them. When the file changes on disk it is
/// reread after a short delay; an error is shown while the previous content stays. Every viewer has Refresh and Open in
/// external app; SVG also has Open as text.
/// </summary>
public abstract partial class ViewerViewModel : ObservableObject, IDisposable
{
    /// <summary>Delay after a file change: programs write through a temporary file and a rename.</summary>
    public static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(300);

    private readonly ITimer _reload;
    private CancellationTokenSource? _loading;
    private Task? _firstLoad;
    private bool _disposed;

    protected ViewerViewModel(string filePath, ViewerKind kind, ViewerContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(context);
        FilePath = filePath;
        Kind = kind;
        Context = context;
        _reload = context.Time.CreateTimer(_ => context.Dispatcher.Post(() => _ = RefreshAsync()), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        context.Workspace.FilesChanged += OnFilesChanged;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public ViewerKind Kind { get; }

    /// <summary>SVG is text: it can be opened in the text editor for editing.</summary>
    public bool CanOpenAsText => Kind == ViewerKind.Svg;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>File info: "1920 × 1080 · PNG · 2.4 MB".</summary>
    [ObservableProperty]
    public partial string Summary { get; protected set; } = string.Empty;

    /// <summary>A display note (image reduced, first animation frame shown), or <c>null</c>.</summary>
    [ObservableProperty]
    public partial string? Note { get; protected set; }

    protected ViewerContext Context { get; }

    /// <summary>The first load, when the tab is first shown; repeated calls await the same load.</summary>
    public Task EnsureLoadedAsync() => _firstLoad ??= RefreshAsync();

    /// <summary>The tab was shown or hidden: the first show reads the file.</summary>
    public virtual void SetShown(bool shown)
    {
        if (shown)
        {
            _ = EnsureLoadedAsync();
        }
    }

    /// <summary>Rereads the file, cancelling a load in progress.</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_disposed)
        {
            return;
        }

        _firstLoad ??= Task.CompletedTask;
        _loading?.Cancel();
        var loading = _loading = new CancellationTokenSource();
        IsLoading = true;
        try
        {
            var content = await Task.Run(() => Read(loading.Token), loading.Token);
            if (!loading.IsCancellationRequested && !_disposed)
            {
                Error = null;
                Show(content);
            }
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // An arbitrary file can break the decoder in any way: the tab shows an error instead of crashing the editor.
            if (!_disposed)
            {
                Error = Describe(exception);
                OnFailed(exception);
            }
        }
        finally
        {
            if (ReferenceEquals(_loading, loading))
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    public void OpenExternal()
    {
        try
        {
            Context.Shell.OpenWithDefaultApp(FilePath);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            Error = Format(Strings.OpenExternalFailed, exception.Message);
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Reads the file on a background thread.</summary>
    protected abstract object Read(CancellationToken cancellationToken);

    /// <summary>Shows what was read, on the UI thread.</summary>
    protected abstract void Show(object content);

    /// <summary>The read error text for the user.</summary>
    protected virtual string Describe(Exception exception) =>
        Format(Strings.ViewerLoadFailed, exception is FileNotFoundException or DirectoryNotFoundException ? Strings.FileMissing : exception.Message);

    /// <summary>The read failed; <see cref="Error"/> is already shown.</summary>
    protected virtual void OnFailed(Exception exception)
    {
    }

    /// <summary>An error reported by the page or a byte read; <c>null</c> clears it.</summary>
    protected void ShowError(string? message) => Error = message;

    protected static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        _disposed = true;
        Context.Workspace.FilesChanged -= OnFilesChanged;
        _reload.Dispose();
        _loading?.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanOpenAsText))]
    private async Task OpenAsTextAsync() => await Context.Commands.ExecuteAsync(ViewerCommands.OpenAsTextId, FilePath);

    // Changes arrive in batches from a background thread; a batch with this file (re)schedules the reread.
    private void OnFilesChanged(object? sender, FileChangesEventArgs e)
    {
        if (_firstLoad is not null && (e.RequiresRescan || e.Changes.Any(change => string.Equals(change.Path, FilePath, StringComparison.OrdinalIgnoreCase))))
        {
            _reload.Change(ReloadDelay, Timeout.InfiniteTimeSpan);
        }
    }
}
