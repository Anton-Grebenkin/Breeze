using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Documents.Commands;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using CodeEditor.Modules.Documents.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Documents.ViewModels;

/// <summary>
/// Document viewer in an editor tab (ADR 0034): the file is read and parsed in the background when the tab is first
/// shown, so the UI doesn't wait for a large file. When the file changes on disk (agent, Word, another program), it's
/// reloaded after a short pause; a read error is shown and the previous content stays. Every viewer has Refresh and
/// Open in External App.
/// </summary>
public abstract partial class DocumentViewerViewModel : ObservableObject, IDisposable
{
    /// <summary>Pause after a file change: Word and the agent write via a temp file and a rename.</summary>
    public static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(300);

    private readonly ITimer _reload;
    private CancellationTokenSource? _loading;
    private Task? _firstLoad;
    private bool _disposed;

    protected DocumentViewerViewModel(string filePath, DocumentViewerContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(context);
        FilePath = filePath;
        Context = context;
        _reload = context.Time.CreateTimer(_ => context.Dispatcher.Post(() => _ = RefreshAsync()), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        context.Workspace.FilesChanged += OnFilesChanged;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public DocumentKind Kind => DocumentKinds.Of(FilePath);

    /// <summary>CSV can also be opened as text for editing.</summary>
    public bool CanOpenAsText => Kind == DocumentKind.Csv;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Short document info: "12 slides", "120 rows, 8 columns".</summary>
    [ObservableProperty]
    public partial string Summary { get; protected set; } = string.Empty;

    protected DocumentViewerContext Context { get; }

    /// <summary>The first load happens when the tab is first shown; repeated calls await the same load.</summary>
    public Task EnsureLoadedAsync() => _firstLoad ??= RefreshAsync();

    /// <summary>Reloads the file, cancelling a load in progress.</summary>
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
                Show(content);
                Error = null;
            }
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (DocumentErrors.IsReadFailure(exception))
        {
            // A third-party file can break parsing in any way: the tab shows the error instead of crashing the editor.
            if (!_disposed)
            {
                Error = Format(Strings.ViewerLoadFailed, exception.Message);
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

    [RelayCommand(CanExecute = nameof(CanOpenAsText))]
    private async Task OpenAsTextAsync() => await Context.Commands.ExecuteAsync(DocumentCommands.OpenAsTextId, FilePath);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Reads and parses the file on a background thread.</summary>
    protected abstract object Read(CancellationToken cancellationToken);

    /// <summary>Shows the parsed content on the UI thread.</summary>
    protected abstract void Show(object content);

    protected byte[] ReadBytes() => Context.FileSystem.ReadAllBytes(FilePath);

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

    // Changes arrive in batches on a background thread; a batch with this file schedules a reload after the pause.
    private void OnFilesChanged(object? sender, FileChangesEventArgs e)
    {
        if (_firstLoad is not null && (e.RequiresRescan || e.Changes.Any(change => string.Equals(change.Path, FilePath, StringComparison.OrdinalIgnoreCase))))
        {
            _reload.Change(ReloadDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
