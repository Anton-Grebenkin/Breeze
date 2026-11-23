using System.Collections.Immutable;

namespace CodeEditor.Modules.Browser.Services.Tabs;

/// <summary>
/// The browser's tabs (ADR 0054): several pages in one profile, one of them shown. The user's tabs come back after a
/// restart (<see cref="BrowserTabStore"/>) and load when first shown; the agent's tabs are not kept. A window a page
/// opens (a link "in a new window", a sign-in popup) becomes a tab next to its opener, and closing it returns there.
/// Thread-safe: the agent works from its own thread; events are raised on the thread that made the change.
/// </summary>
public sealed class BrowserTabs : IDisposable
{
    private readonly IBrowserEngine _engine;
    private readonly BrowserTabStore? _store;
    private readonly Lock _gate = new();
    private ImmutableArray<BrowserTab> _tabs = [];
    private BrowserTab? _active;
    private int _lastId;
    private volatile bool _restored;

    public BrowserTabs(IBrowserEngine engine, BrowserTabStore? store = null)
    {
        _engine = engine;
        _store = store;
        if (_store is not null)
        {
            _store.FolderChanged += OnFolderChanged;
        }
    }

    /// <summary>The list, the order, a pin or the shown tab changed.</summary>
    public event EventHandler? Changed;

    /// <summary>A tab's page changed its address, title, icon or loading state.</summary>
    public event EventHandler<BrowserTabEventArgs>? PageChanged;

    public event EventHandler<BrowserTabEventArgs>? Opened;

    /// <summary>Tabs in strip order: pinned first.</summary>
    public ImmutableArray<BrowserTab> Tabs => Snapshot().Tabs;

    /// <summary>The shown tab; <c>null</c> when there are none.</summary>
    public BrowserTab? Active => Snapshot().Active;

    public BrowserTab? Find(string id) => Tabs.FirstOrDefault(tab => tab.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>A new empty tab: after its <paramref name="opener"/>, or last.</summary>
    public BrowserTab Open(BrowserTabOwner owner = BrowserTabOwner.User, bool select = true, BrowserTab? opener = null)
    {
        EnsureRestored();
        BrowserTab tab;
        lock (_gate)
        {
            tab = Create(owner, opener?.Id, restored: null);
            var pinned = _tabs.Count(static other => other.IsPinned);
            var after = opener is null ? -1 : _tabs.IndexOf(opener);
            _tabs = _tabs.Insert(after < 0 ? _tabs.Length : Math.Max(after + 1, pinned), tab);
            if (select || _active is null)
            {
                _active = tab;
            }
        }

        Opened?.Invoke(this, new BrowserTabEventArgs(tab));
        OnChanged();
        return tab;
    }

    public void Select(BrowserTab tab)
    {
        lock (_gate)
        {
            if (_active == tab || !_tabs.Contains(tab))
            {
                return;
            }

            _active = tab;
        }

        OnChanged();
    }

    /// <summary>Shows the next (<paramref name="step"/> 1) or the previous (-1) tab, around the ends.</summary>
    public void SelectNext(int step)
    {
        lock (_gate)
        {
            if (_tabs.Length < 2 || _active is null)
            {
                return;
            }

            _active = _tabs[((_tabs.IndexOf(_active) + step) % _tabs.Length + _tabs.Length) % _tabs.Length];
        }

        OnChanged();
    }

    /// <summary>Closes the tab; a shown one hands over to its opener, else to its neighbour.</summary>
    public void Close(BrowserTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        lock (_gate)
        {
            var index = _tabs.IndexOf(tab);
            if (index < 0)
            {
                return;
            }

            _tabs = _tabs.RemoveAt(index);
            if (_active == tab)
            {
                _active = _tabs.FirstOrDefault(other => other.Id == tab.OpenerId) ?? (_tabs.IsEmpty ? null : _tabs[Math.Min(index, _tabs.Length - 1)]);
            }
        }

        Release(tab);
        OnChanged();
    }

    /// <summary>Closes every tab that is neither <paramref name="keep"/> nor pinned.</summary>
    public void CloseOthers(BrowserTab keep)
    {
        foreach (var tab in Tabs.Where(tab => tab != keep && !tab.IsPinned))
        {
            Close(tab);
        }
    }

    /// <summary>A pinned tab joins the pinned ones at the start; an unpinned one comes right after them.</summary>
    public void SetPinned(BrowserTab tab, bool pinned)
    {
        ArgumentNullException.ThrowIfNull(tab);
        lock (_gate)
        {
            if (tab.IsPinned == pinned || !_tabs.Contains(tab))
            {
                return;
            }

            tab.IsPinned = pinned;
            var rest = _tabs.Remove(tab);
            _tabs = rest.Insert(rest.Count(static other => other.IsPinned), tab);
        }

        OnChanged();
    }

    public void Dispose()
    {
        if (_store is not null)
        {
            _store.FolderChanged -= OnFolderChanged;
        }

        ImmutableArray<BrowserTab> tabs;
        lock (_gate)
        {
            (tabs, _tabs, _active) = (_tabs, [], null);
        }

        foreach (var tab in tabs)
        {
            Release(tab);
        }
    }

    private (ImmutableArray<BrowserTab> Tabs, BrowserTab? Active) Snapshot()
    {
        EnsureRestored();
        lock (_gate)
        {
            return (_tabs, _active);
        }
    }

    private BrowserTab Create(BrowserTabOwner owner, string? openerId, BrowserPageState? restored)
    {
        var page = _engine.CreatePage(restored);
        page.StateChanged += OnPageStateChanged;
        page.PopupRequested += OnPopupRequested;
        page.CloseRequested += OnCloseRequested;
        return new BrowserTab("t" + ++_lastId, page, owner, openerId);
    }

    private void Release(BrowserTab tab)
    {
        tab.Page.StateChanged -= OnPageStateChanged;
        tab.Page.PopupRequested -= OnPopupRequested;
        tab.Page.CloseRequested -= OnCloseRequested;
        tab.Page.Dispose();
    }

    private void EnsureRestored()
    {
        if (_restored)
        {
            return;
        }

        lock (_gate)
        {
            if (_restored)
            {
                return;
            }

            // Set first: a page that reports its state while being created must not start the restore again.
            _restored = true;
            foreach (var record in _store?.Load() ?? [])
            {
                if (!Uri.TryCreate(record.Url, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
                {
                    continue;
                }

                var tab = Create(BrowserTabOwner.User, openerId: null, new BrowserPageState(url, record.Title));
                tab.IsPinned = record.IsPinned;
                _tabs = _tabs.Add(tab);
                _active = record.IsActive ? tab : _active;
            }

            _active ??= _tabs.FirstOrDefault();
        }
    }

    private BrowserTab? Of(object? page) => Tabs.FirstOrDefault(tab => ReferenceEquals(tab.Page, page));

    private void OnPageStateChanged(object? sender, EventArgs e)
    {
        if (Of(sender) is { } tab)
        {
            PageChanged?.Invoke(this, new BrowserTabEventArgs(tab));
            Save();
        }
    }

    // The window belongs to whoever owns the page that opened it, and is shown if that page was.
    private void OnPopupRequested(object? sender, BrowserPopupEventArgs e)
    {
        if (Of(sender) is { } opener)
        {
            e.Page = Open(opener.Owner, select: opener == Active, opener).Page;
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (Of(sender) is { } tab)
        {
            Close(tab);
        }
    }

    // Another folder: its tabs are read on the next use, and nothing is saved in between.
    private void OnFolderChanged(object? sender, EventArgs e)
    {
        ImmutableArray<BrowserTab> closed;
        lock (_gate)
        {
            if (!_restored)
            {
                return;
            }

            (closed, _tabs, _active, _restored) = (_tabs, [], null, false);
        }

        foreach (var tab in closed)
        {
            Release(tab);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        Save();
    }

    private void Save()
    {
        if (_store is null)
        {
            return;
        }

        var (tabs, active) = Snapshot();
        _store.Save([.. tabs.Where(static tab => !tab.IsAgent && tab.HasWebPage)
            .Select(tab => new BrowserTabRecord(tab.Page.Url!.AbsoluteUri, tab.Page.Title, tab.IsPinned, tab == active))]);
    }
}
