using System.Collections;
using System.Collections.Specialized;
using CodeEditor.Modules.Viewers.Hex;
using CodeEditor.Modules.Viewers.Tests.Infrastructure;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// Dump rows for a virtualized list: a multi-gigabyte file is never read whole, a row waits for its page and fills in
/// when it arrives; rows are found by index; a changed file is rebuilt.
/// </summary>
public sealed class HexRowsTests : IDisposable
{
    private const string Path = @"C:\work\disk.img";
    private const long FourGigabytes = 4L * 1024 * 1024 * 1024;

    private readonly MemoryFileBytes _bytes = new();
    private readonly QueueDispatcher _dispatcher = new();
    private readonly HexPages _pages;
    private readonly HexRows _rows;

    public HexRowsTests()
    {
        _bytes.AddGenerated(Path, FourGigabytes + 5);
        _pages = new HexPages(Path, _bytes, _dispatcher);
        _rows = new HexRows(_pages, "Смещение");
        _rows.Reload(FourGigabytes + 5);
    }

    public void Dispose() => _pages.Dispose();

    [Fact]
    public void HugeFile_HasAllRows_WithoutReadingAnything()
    {
        Assert.Equal((int)(FourGigabytes / HexFormatter.BytesPerRow) + 1, _rows.Count);
        Assert.Empty(_bytes.Reads);
        Assert.False(_rows.IsTruncated);
    }

    [Fact]
    public async Task Row_WaitsForItsPage_AndIsFilledWhenItArrives()
    {
        var row = _rows[1000];
        Assert.Equal(string.Empty, row.Hex);

        await _dispatcher.RunNextAsync();

        Assert.Equal(HexFormatter.Hex(Expected(row.Offset, HexFormatter.BytesPerRow)), row.Hex);
        Assert.Equal(HexFormatter.Text(Expected(row.Offset, HexFormatter.BytesPerRow)), row.Text);
        var read = Assert.Single(_bytes.Reads);
        Assert.Equal(0, read.Offset);
    }

    [Fact]
    public async Task RowsOfALoadedPage_AreFilledAtOnce()
    {
        _ = _rows[0];
        await _dispatcher.RunNextAsync();

        var neighbour = _rows[1];

        Assert.NotEqual(string.Empty, neighbour.Hex);
        Assert.Single(_bytes.Reads);
    }

    [Fact]
    public async Task LastRow_IsShort_AndOffsetsHaveNineDigits()
    {
        var last = _rows[_rows.Count - 1];
        await _dispatcher.RunNextAsync();

        Assert.Equal("100000000", last.OffsetText);
        Assert.Equal(5, last.Text.Length);
        Assert.Equal("Смещение ", _rows.OffsetHeader);
    }

    [Fact]
    public void Rows_AreFoundByNumber()
    {
        IList list = _rows;

        Assert.Equal(_rows[42], _rows[42]);
        Assert.Equal(42, list.IndexOf(_rows[42]));
        Assert.True(list.Contains(_rows[7]));
        Assert.Equal(-1, list.IndexOf("строка"));
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add(_rows[0]));
    }

    [Fact]
    public void Reload_RebuildsTheList()
    {
        var resets = 0;
        _rows.CollectionChanged += (_, e) => resets += e.Action == NotifyCollectionChangedAction.Reset ? 1 : 0;

        _rows.Reload(32);

        Assert.Equal(1, resets);
        Assert.Equal(2, _rows.Count);
        Assert.Equal("Смещение", _rows.OffsetHeader);
    }

    [Fact]
    public void Mark_PointsAtTheByteInItsRow()
    {
        _rows.Mark(0x1F43);

        Assert.Equal(3, _rows[0x1F4].Marked);
        Assert.Equal(HexRow.NoMark, _rows[0x1F5].Marked);
    }

    [Fact]
    public void FileOverThirtyTwoGigabytes_IsShownInPart()
    {
        _rows.Reload(40L * 1024 * 1024 * 1024);

        Assert.True(_rows.IsTruncated);
        Assert.Equal(int.MaxValue, _rows.Count);
    }

    [Fact]
    public void EmptyFile_HasNoRows()
    {
        _rows.Reload(0);

        Assert.Empty(_rows);
        Assert.Throws<ArgumentOutOfRangeException>(() => _rows[0]);
    }

    private static byte[] Expected(long offset, int count) => [.. Enumerable.Range(0, count).Select(index => MemoryFileBytes.At(offset + index))];
}
