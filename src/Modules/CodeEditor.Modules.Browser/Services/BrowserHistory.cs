using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Pages opened in the browser panel, by the user or the agent, newest first, kept in <c>browser-history.json</c> in user
/// data: the address bar menu and "Browser: Recent Pages" offer them again. Cookies, the page cache and site storage
/// already live in the browser profile (<c>WebView2</c> in user data). The file is read on first use and written in the
/// background, in order.
/// </summary>
public sealed partial class BrowserHistory : IDisposable
{
    public const int Capacity = 200;
    public const string FileName = "browser-history.json";

    private readonly IBrowserEngine _browser;
    private readonly IFileSystem _fileSystem;
    private readonly UserDataPaths _paths;
    private readonly TimeProvider _time;
    private readonly ILogger<BrowserHistory> _logger;
    private readonly Lock _gate = new();
    private List<BrowserVisit>? _visits;
    private Task _writing = Task.CompletedTask;

    public BrowserHistory(IBrowserEngine browser, IFileSystem fileSystem, UserDataPaths paths, TimeProvider time, ILogger<BrowserHistory> logger)
    {
        _browser = browser;
        _fileSystem = fileSystem;
        _paths = paths;
        _time = time;
        _logger = logger;
        _browser.StateChanged += OnStateChanged;
    }

    /// <summary>The history changed; raised on the thread that changed it.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<BrowserVisit> Visits
    {
        get
        {
            lock (_gate)
            {
                return [.. Load()];
            }
        }
    }

    /// <summary>Completes when the history written so far is on disk (tests, shutdown).</summary>
    public Task Saved => _writing;

    public void Clear()
    {
        lock (_gate)
        {
            _visits = [];
            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _browser.StateChanged -= OnStateChanged;

    // A page counts once it has loaded; its title often arrives a moment later and updates the entry.
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (_browser.IsLoading || _browser.Url is not { Scheme: "http" or "https" } url)
        {
            return;
        }

        bool changed;
        lock (_gate)
        {
            changed = Record(url.AbsoluteUri, _browser.Title);
            if (changed)
            {
                Save();
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    // A visit moves the address to the top; the same page again with the same title changes nothing.
    private bool Record(string url, string title)
    {
        var visits = Load();
        if (visits.Count > 0 && visits[0].Url == url && (title.Length == 0 || visits[0].Title == title))
        {
            return false;
        }

        var index = visits.FindIndex(visit => visit.Url == url);
        var known = index >= 0 ? visits[index].Title : string.Empty;
        if (index >= 0)
        {
            visits.RemoveAt(index);
        }

        visits.Insert(0, new BrowserVisit(url, title.Length > 0 ? title : known, _time.GetUtcNow()));
        if (visits.Count > Capacity)
        {
            visits.RemoveRange(Capacity, visits.Count - Capacity);
        }

        return true;
    }

    private List<BrowserVisit> Load()
    {
        if (_visits is not null)
        {
            return _visits;
        }

        var path = _paths.File(FileName);
        try
        {
            _visits = _fileSystem.FileExists(path) ? JsonSerializer.Deserialize(_fileSystem.ReadAllText(path), BrowserJsonContext.Default.ListBrowserVisit) ?? [] : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LogStorageFailed(_logger, exception, path);
            _visits = [];
        }

        return _visits;
    }

    // Writes go one after another, so an older list never overwrites a newer one.
    private void Save()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(_visits ?? [], BrowserJsonContext.Default.ListBrowserVisit);
        _writing = _writing.ContinueWith(_ => Write(bytes), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private void Write(byte[] bytes)
    {
        var path = _paths.File(FileName);
        try
        {
            if (!_fileSystem.DirectoryExists(_paths.Root))
            {
                _fileSystem.CreateDirectory(_paths.Root);
            }

            _fileSystem.WriteAllBytesAtomic(path, bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogStorageFailed(_logger, exception, path);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read or save the browser history {Path}")]
    private static partial void LogStorageFailed(ILogger logger, Exception exception, string path);
}
