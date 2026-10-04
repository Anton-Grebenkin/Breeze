using CodeEditor.Core.Threading;
using CodeEditor.Modules.Viewers.Services;

namespace CodeEditor.Modules.Viewers.Hex;

/// <summary>
/// A file read in pages for the hex view: a page is 64 KB (below the large object heap threshold), the last 16 pages are
/// cached (1 MB). A page missing from the cache is read in the background and arrives via <see cref="Loaded"/> on the UI
/// thread; requesting a page that is already being read does not start a second read. When the file changes,
/// <see cref="Reset"/> forgets what was read, and reads already in flight no longer reach the cache. UI thread only.
/// </summary>
public sealed class HexPages : IDisposable
{
    public const int PageSize = 64 * 1024;
    public const int CachedPages = 16;

    private readonly string _path;
    private readonly IFileBytes _bytes;
    private readonly IUiDispatcher _dispatcher;
    private readonly HexPageCache _cache = new(CachedPages);
    private readonly Dictionary<long, Task> _reading = [];
    private CancellationTokenSource _cancel = new();
    private int _generation;
    private bool _disposed;

    public HexPages(string path, IFileBytes bytes, IUiDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _path = path;
        _bytes = bytes;
        _dispatcher = dispatcher;
    }

    /// <summary>A page was read and is in the cache. Raised on the UI thread.</summary>
    public event EventHandler<long>? Loaded;

    /// <summary>A page failed to read; the argument is a message for the user. Raised on the UI thread.</summary>
    public event EventHandler<string>? Failed;

    public static long PageOf(long offset) => offset / PageSize;

    /// <summary>A page from the cache, without reading the disk.</summary>
    public bool TryGetCached(long page, out byte[] data) => _cache.TryGet(page, out data);

    /// <summary>Reads the page in the background unless it is cached. Reads nothing after the tab is closed.</summary>
    /// <returns>A task that completes when the page is in the cache (or the read failed).</returns>
    public Task LoadAsync(long page)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        if (_reading.TryGetValue(page, out var pending))
        {
            return pending;
        }

        if (_cache.TryGet(page, out _))
        {
            return Task.CompletedTask;
        }

        // Registered before starting: the result may arrive before this method returns.
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _reading[page] = done.Task;
        var generation = _generation;
        var cancellation = _cancel.Token;
        _ = Task.Run(() => Read(page, generation, done, cancellation), CancellationToken.None);
        return done.Task;
    }

    /// <summary>The file changed: forgets what was read and cancels reads in flight.</summary>
    public void Reset()
    {
        if (_disposed)
        {
            return;
        }

        _cancel.Cancel();
        _cancel.Dispose();
        _cancel = new CancellationTokenSource();
        _generation++;
        _reading.Clear();
        _cache.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancel.Cancel();
        _cancel.Dispose();
        _reading.Clear();
        _cache.Clear();
    }

    // Background thread: only the disk read; the cache and events go through the dispatcher to the UI thread.
    private void Read(long page, int generation, TaskCompletionSource done, CancellationToken cancellation)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var data = ReadPage(page);
            _dispatcher.Post(() => Complete(page, generation, data, done));
        }
        catch (OperationCanceledException)
        {
            done.TrySetCanceled(cancellation);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _dispatcher.Post(() => Fail(page, generation, exception.Message, done));
        }
    }

    private byte[] ReadPage(long page)
    {
        var buffer = new byte[PageSize];
        var count = _bytes.Read(_path, page * PageSize, buffer);
        return count == PageSize ? buffer : buffer[..count];
    }

    private void Complete(long page, int generation, byte[] data, TaskCompletionSource done)
    {
        if (IsCurrent(generation))
        {
            _reading.Remove(page);
            _cache.Add(page, data);
            Loaded?.Invoke(this, page);
        }

        done.TrySetResult();
    }

    private void Fail(long page, int generation, string message, TaskCompletionSource done)
    {
        if (IsCurrent(generation))
        {
            _reading.Remove(page);
            Failed?.Invoke(this, message);
        }

        done.TrySetResult();
    }

    private bool IsCurrent(int generation) => !_disposed && generation == _generation;
}
