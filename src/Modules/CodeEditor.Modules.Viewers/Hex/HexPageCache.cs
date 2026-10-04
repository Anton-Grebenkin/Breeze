namespace CodeEditor.Modules.Viewers.Hex;

/// <summary>
/// The most recently read file pages, so scrolling back and forth does not reread the disk. The least recently used
/// page is evicted first (LRU). Not thread-safe: UI thread only.
/// </summary>
/// <remarks>Lookup, add and eviction are O(1): a node dictionary plus a recency list.</remarks>
public sealed class HexPageCache(int capacity)
{
    private readonly Dictionary<long, LinkedListNode<(long Page, byte[] Data)>> _pages = [];
    private readonly LinkedList<(long Page, byte[] Data)> _recent = [];

    public int Capacity { get; } = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "The cache needs room for a page.");

    public int Count => _pages.Count;

    public bool TryGet(long page, out byte[] data)
    {
        if (!_pages.TryGetValue(page, out var node))
        {
            data = [];
            return false;
        }

        _recent.Remove(node);
        _recent.AddFirst(node);
        data = node.Value.Data;
        return true;
    }

    public void Add(long page, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (_pages.Remove(page, out var existing))
        {
            _recent.Remove(existing);
        }

        _pages[page] = _recent.AddFirst((page, data));
        if (_pages.Count > Capacity)
        {
            var oldest = _recent.Last!;
            _recent.RemoveLast();
            _pages.Remove(oldest.Value.Page);
        }
    }

    public void Clear()
    {
        _pages.Clear();
        _recent.Clear();
    }
}
