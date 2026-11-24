using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Browser.Services.Tabs;

/// <summary>
/// Sites the browser knows (ADR 0054): the ones the user opened in a tab of theirs and the ones the user let the agent
/// open once. The agent opens a known site without asking and is told their names, so "open GitLab" needs no address
/// and no card. A page the agent reached by itself — a link it followed — does not make a site known. Kept in
/// <c>browser-sites.json</c> in user data, most recent first; written in the background, in order.
/// </summary>
public sealed partial class BrowserKnownSites : IDisposable
{
    public const int Capacity = 200;
    public const string FileName = "browser-sites.json";

    private readonly BrowserTabs _tabs;
    private readonly IFileSystem _fileSystem;
    private readonly UserDataPaths _paths;
    private readonly ILogger<BrowserKnownSites> _logger;
    private readonly Lock _gate = new();
    private List<string>? _hosts;
    private Task _writing = Task.CompletedTask;

    public BrowserKnownSites(BrowserTabs tabs, IFileSystem fileSystem, UserDataPaths paths, ILogger<BrowserKnownSites> logger)
    {
        _tabs = tabs;
        _fileSystem = fileSystem;
        _paths = paths;
        _logger = logger;
        _tabs.PageChanged += OnPageChanged;
    }

    /// <summary>Known sites, the most recently used first.</summary>
    public IReadOnlyList<string> Hosts
    {
        get
        {
            lock (_gate)
            {
                return [.. Load()];
            }
        }
    }

    /// <summary>Completes when the sites remembered so far are on disk (tests, shutdown).</summary>
    public Task Saved => _writing;

    public bool IsKnown(string host)
    {
        lock (_gate)
        {
            return Load().Contains(host, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>The site was opened by the user or with their approval: it moves to the top of the known ones.</summary>
    public void Remember(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.Scheme is not ("http" or "https") || BrowserApprovals.IsLocal(url.IdnHost))
        {
            return;
        }

        lock (_gate)
        {
            var hosts = Load();
            if (hosts.Count > 0 && hosts[0].Equals(url.IdnHost, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            hosts.RemoveAll(host => host.Equals(url.IdnHost, StringComparison.OrdinalIgnoreCase));
            hosts.Insert(0, url.IdnHost);
            if (hosts.Count > Capacity)
            {
                hosts.RemoveRange(Capacity, hosts.Count - Capacity);
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(hosts, BrowserJsonContext.Default.ListString);
            _writing = _writing.ContinueWith(_ => Write(bytes), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    public void Dispose() => _tabs.PageChanged -= OnPageChanged;

    // A page that loaded, with an answer from its server, in a tab of the user's.
    private void OnPageChanged(object? sender, BrowserTabEventArgs e)
    {
        var page = e.Tab.Page;
        if (!e.Tab.IsAgent && !page.IsLoading && page.StatusCode is not null && page.Url is { } url)
        {
            Remember(url);
        }
    }

    private List<string> Load()
    {
        if (_hosts is not null)
        {
            return _hosts;
        }

        var path = _paths.File(FileName);
        try
        {
            _hosts = _fileSystem.FileExists(path) ? JsonSerializer.Deserialize(_fileSystem.ReadAllText(path), BrowserJsonContext.Default.ListString) ?? [] : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LogStorageFailed(_logger, exception, path);
            _hosts = [];
        }

        return _hosts;
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read or save the known sites {Path}")]
    private static partial void LogStorageFailed(ILogger logger, Exception exception, string path);
}
