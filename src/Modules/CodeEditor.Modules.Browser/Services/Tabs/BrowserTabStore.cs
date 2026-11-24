using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.Shell.Session;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Browser.Services.Tabs;

/// <summary>
/// The user's browser tabs between runs: <c>browser-tabs/&lt;folder key&gt;.json</c> in user data, one file per opened
/// folder, like the editor's own tabs. A window is a process with one folder, so two windows never write one file.
/// Written in the background, in order; an unchanged list is not written again.
/// </summary>
public sealed partial class BrowserTabStore : IDisposable
{
    public const string FolderName = "browser-tabs";

    private const string NoFolderKey = "no-folder";

    private readonly IFileSystem _fileSystem;
    private readonly UserDataPaths _paths;
    private readonly IWorkspace _workspace;
    private readonly ILogger<BrowserTabStore> _logger;
    private readonly Lock _gate = new();
    private (string Path, byte[] Bytes)? _last;
    private Task _writing = Task.CompletedTask;

    public BrowserTabStore(IFileSystem fileSystem, UserDataPaths paths, IWorkspace workspace, ILogger<BrowserTabStore> logger)
    {
        _fileSystem = fileSystem;
        _paths = paths;
        _workspace = workspace;
        _logger = logger;
        _workspace.Changed += OnWorkspaceChanged;
    }

    /// <summary>Another folder was opened: its tabs replace the current ones.</summary>
    public event EventHandler? FolderChanged;

    /// <summary>Completes when the tabs saved so far are on disk (tests, shutdown).</summary>
    public Task Saved => _writing;

    public IReadOnlyList<BrowserTabRecord> Load()
    {
        var path = FilePath();
        try
        {
            return _fileSystem.FileExists(path) ? JsonSerializer.Deserialize(_fileSystem.ReadAllBytes(path), BrowserJsonContext.Default.ListBrowserTabRecord) ?? [] : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LogStorageFailed(_logger, exception, path);
            return [];
        }
    }

    public void Save(IReadOnlyList<BrowserTabRecord> tabs)
    {
        var path = FilePath();
        var bytes = JsonSerializer.SerializeToUtf8Bytes([.. tabs], BrowserJsonContext.Default.ListBrowserTabRecord);
        lock (_gate)
        {
            if (_last is var (lastPath, lastBytes) && lastPath == path && lastBytes.AsSpan().SequenceEqual(bytes))
            {
                return;
            }

            _last = (path, bytes);
            _writing = _writing.ContinueWith(_ => Write(path, bytes), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    public void Dispose() => _workspace.Changed -= OnWorkspaceChanged;

    private string FilePath() =>
        Path.Combine(_paths.File(FolderName), (_workspace.Root is { } root ? JsonSessionStore.FolderKey(root) : NoFolderKey) + ".json");

    private void OnWorkspaceChanged(object? sender, EventArgs e) => FolderChanged?.Invoke(this, EventArgs.Empty);

    private void Write(string path, byte[] bytes)
    {
        try
        {
            var folder = Path.GetDirectoryName(path)!;
            if (!_fileSystem.DirectoryExists(folder))
            {
                _fileSystem.CreateDirectory(folder);
            }

            _fileSystem.WriteAllBytesAtomic(path, bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogStorageFailed(_logger, exception, path);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read or save the browser tabs {Path}")]
    private static partial void LogStorageFailed(ILogger logger, Exception exception, string path);
}
