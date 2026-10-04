using CodeEditor.Modules.Viewers.Hex;
using CodeEditor.Modules.Viewers.Tests.Infrastructure;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// File pages for the dump: a cache of recent pages, background reads with results on the UI thread, one read per page,
/// reads forgotten after the file changes.
/// </summary>
public sealed class HexPagesTests : IDisposable
{
    private const string Path = @"C:\work\big.bin";

    private readonly MemoryFileBytes _bytes = new();
    private readonly QueueDispatcher _dispatcher = new();
    private readonly HexPages _pages;

    public HexPagesTests()
    {
        _bytes.AddGenerated(Path, 10L * HexPages.PageSize + 100);
        _pages = new HexPages(Path, _bytes, _dispatcher);
    }

    public void Dispose() => _pages.Dispose();

    [Fact]
    public void Cache_EvictsTheLeastRecentlyUsedPage()
    {
        var cache = new HexPageCache(2);
        cache.Add(1, [1]);
        cache.Add(2, [2]);

        cache.TryGet(1, out _);
        cache.Add(3, [3]);

        Assert.True(cache.TryGet(1, out _));
        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(3, out var third));
        Assert.Equal([3], third);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public async Task Page_IsReadInTheBackground_AndArrivesInTheUiThread()
    {
        long? loaded = null;
        _pages.Loaded += (_, page) => loaded = page;

        var reading = _pages.LoadAsync(3);
        Assert.Null(loaded);
        await _dispatcher.RunNextAsync();
        await reading;

        Assert.Equal(3, loaded);
        Assert.True(_pages.TryGetCached(3, out var data));
        Assert.Equal(HexPages.PageSize, data.Length);
        Assert.Equal(MemoryFileBytes.At(3L * HexPages.PageSize), data[0]);
    }

    [Fact]
    public async Task SecondRequest_WhileReading_DoesNotReadAgain()
    {
        var first = _pages.LoadAsync(0);
        var second = _pages.LoadAsync(0);
        await _dispatcher.RunNextAsync();
        await Task.WhenAll(first, second);

        Assert.Same(first, second);
        Assert.Single(_bytes.Reads);
    }

    [Fact]
    public async Task LastPage_IsShort()
    {
        var reading = _pages.LoadAsync(10);
        await _dispatcher.RunNextAsync();
        await reading;

        Assert.True(_pages.TryGetCached(10, out var data));
        Assert.Equal(100, data.Length);
    }

    [Fact]
    public async Task Reset_ForgetsPages_AndDropsReadsInFlight()
    {
        var before = _pages.LoadAsync(1);
        await _dispatcher.RunNextAsync();
        await before;

        var stale = _pages.LoadAsync(2);
        _pages.Reset();
        await DrainAsync(stale);

        Assert.False(_pages.TryGetCached(1, out _));
        Assert.False(_pages.TryGetCached(2, out _));
    }

    [Fact]
    public async Task ReadFailure_IsReported()
    {
        string? failure = null;
        _pages.Failed += (_, message) => failure = message;
        _bytes.Failure = new IOException("Диск недоступен.");

        var reading = _pages.LoadAsync(0);
        await _dispatcher.RunNextAsync();
        await reading;

        Assert.Equal("Диск недоступен.", failure);
        Assert.False(_pages.TryGetCached(0, out _));
    }

    [Fact]
    public void Disposed_Pages_DoNotRead()
    {
        _pages.Dispose();

        Assert.True(_pages.LoadAsync(0).IsCompleted);
        Assert.Empty(_bytes.Reads);
    }

    // A read cancelled before it starts completes at once; one already read arrives on the UI thread and is dropped there.
    private async Task DrainAsync(Task stale)
    {
        try
        {
            await stale.WaitAsync(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }
        catch (TimeoutException)
        {
            await _dispatcher.RunNextAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }
}
