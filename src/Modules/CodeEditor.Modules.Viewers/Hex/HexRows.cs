using System.Collections;
using System.Collections.Specialized;

namespace CodeEditor.Modules.Viewers.Hex;

/// <summary>
/// Dump rows for a virtualized list: a row is created only when the list shows it, so a multi-gigabyte file keeps
/// neither rows nor bytes in memory. Row bytes come from file pages (<see cref="HexPages"/>); a row whose page is still
/// being read is filled when the page arrives. The list sees at most <see cref="int.MaxValue"/> rows, i.e. 32 GB of
/// the file (<see cref="IsTruncated"/>). UI thread only.
/// </summary>
/// <remarks>The indexer and <see cref="IList.IndexOf"/> are O(1): WPF finds rows by index equality.</remarks>
public sealed class HexRows : IList, IReadOnlyList<HexRow>, INotifyCollectionChanged
{
    private readonly HexPages _pages;
    private readonly string _offsetLabel;
    private readonly Dictionary<long, List<HexRow>> _waiting = [];
    private long _marked = -1;
    private int _digits;

    /// <param name="offsetLabel">The offset column header; offsets are at least as wide so the columns line up.</param>
    public HexRows(HexPages pages, string offsetLabel)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(offsetLabel);
        _pages = pages;
        _offsetLabel = offsetLabel;
        _pages.Loaded += OnPageLoaded;
        Measure(0);
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>File length in bytes.</summary>
    public long Length { get; private set; }

    public int Count { get; private set; }

    /// <summary>The file is larger than 32 GB: only the beginning is shown.</summary>
    public bool IsTruncated { get; private set; }

    /// <summary>The offset column header, as wide as the offsets.</summary>
    public string OffsetHeader { get; private set; } = string.Empty;

    bool IList.IsReadOnly => true;

    bool IList.IsFixedSize => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    public HexRow this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            return Create(index);
        }
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    /// <summary>The index of the row containing the offset.</summary>
    public static int RowOf(long offset) => (int)Math.Min(offset / HexFormatter.BytesPerRow, int.MaxValue);

    /// <summary>The file was reread: new length, new bytes. The list is rebuilt.</summary>
    public void Reload(long length)
    {
        _pages.Reset();
        _waiting.Clear();
        Measure(length);
        Reset();
    }

    /// <summary>Marks the byte reached by go to offset; rows are rebuilt with the mark.</summary>
    public void Mark(long offset)
    {
        _marked = offset;
        Reset();
    }

    public int IndexOf(HexRow row) => row is not null && row.Index < Count ? row.Index : -1;

    public IEnumerator<HexRow> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return Create(index);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    int IList.IndexOf(object? value) => value is HexRow row ? IndexOf(row) : -1;

    bool IList.Contains(object? value) => value is HexRow row && IndexOf(row) >= 0;

    void ICollection.CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);
        foreach (var row in this)
        {
            array.SetValue(row, index++);
        }
    }

    int IList.Add(object? value) => throw new NotSupportedException();

    void IList.Clear() => throw new NotSupportedException();

    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    void IList.Remove(object? value) => throw new NotSupportedException();

    void IList.RemoveAt(int index) => throw new NotSupportedException();

    private void Measure(long length)
    {
        Length = length;
        var rows = (length + HexFormatter.BytesPerRow - 1) / HexFormatter.BytesPerRow;
        IsTruncated = rows > int.MaxValue;
        Count = (int)Math.Min(rows, int.MaxValue);
        _digits = Math.Max(HexFormatter.OffsetDigits(length), _offsetLabel.Length);
        OffsetHeader = _offsetLabel.PadRight(_digits);
    }

    private HexRow Create(int index)
    {
        var offset = (long)index * HexFormatter.BytesPerRow;
        var marked = _marked >= offset && _marked < offset + HexFormatter.BytesPerRow ? (int)(_marked - offset) : HexRow.NoMark;
        var row = new HexRow(index, HexFormatter.Offset(offset, _digits), marked);
        Fill(row);
        return row;
    }

    // The row starts waiting before the read: the page may arrive before LoadAsync returns.
    private void Fill(HexRow row)
    {
        var page = HexPages.PageOf(row.Offset);
        if (!_pages.TryGetCached(page, out var data))
        {
            Wait(page, row);
            _ = _pages.LoadAsync(page);
            return;
        }

        var start = (int)(row.Offset % HexPages.PageSize);
        var count = (int)Math.Clamp(Math.Min(Length - row.Offset, data.Length - start), 0, HexFormatter.BytesPerRow);
        row.Show(data.AsSpan(start, count));
    }

    private void Wait(long page, HexRow row)
    {
        if (!_waiting.TryGetValue(page, out var rows))
        {
            rows = [];
            _waiting[page] = rows;
        }

        rows.Add(row);
    }

    private void OnPageLoaded(object? sender, long page)
    {
        if (_waiting.Remove(page, out var rows))
        {
            rows.ForEach(Fill);
        }
    }

    private void Reset() => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
}
