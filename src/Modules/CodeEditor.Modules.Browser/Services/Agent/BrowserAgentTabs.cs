using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Tabs;

namespace CodeEditor.Modules.Browser.Services.Agent;

/// <summary>
/// The browser's tabs as the agent uses them (ADR 0054). The agent works in a tab of its own: its first navigation
/// opens one, in the background while the user looks at a page of theirs, and later calls reuse it. A call that names
/// a tab works there; without a tab of its own the agent reads the shown one. When the agent's action opens a window,
/// the agent continues in that tab.
/// </summary>
public sealed partial class BrowserAgentTabs : IDisposable
{
    private static readonly Uri Blank = new("about:blank");

    private readonly BrowserTabs _tabs;
    private readonly ConditionalWeakTable<BrowserTab, HashSet<string>> _links = [];
    private readonly Func<Task>? _showPanel;
    private volatile BrowserTab? _current;
    private volatile BrowserTab? _target;
    private volatile BrowserTab? _opened;

    /// <param name="showPanel">Brings the browser panel to the front without focus; <c>null</c> in tests.</param>
    public BrowserAgentTabs(BrowserTabs tabs, Func<Task>? showPanel = null)
    {
        _tabs = tabs;
        _showPanel = showPanel;
        _tabs.Opened += OnOpened;
    }

    /// <summary>The agent's working tab, if it is still open.</summary>
    public BrowserTab? Current => _current is { } tab && _tabs.Tabs.Contains(tab) ? tab : null;

    /// <summary>
    /// The tab a call would work on, without opening one — for approvals and cards. <c>null</c> when the call would open
    /// a tab of the agent's (<paramref name="opens"/>) or names a tab that does not exist.
    /// </summary>
    public BrowserTab? Peek(string? id, bool opens = false) =>
        !string.IsNullOrWhiteSpace(id) ? _tabs.Find(id.Trim())
        : Current ?? (opens ? null : _tabs.Active);

    /// <summary>The tab a call works on; a navigation without a tab of the agent's opens one.</summary>
    /// <exception cref="AgentToolException">There is no such tab, or nothing is open to read.</exception>
    public BrowserTab Target(string? id, bool opens = false)
    {
        var tab = !string.IsNullOrWhiteSpace(id) ? _tabs.Find(id.Trim()) ?? throw new AgentToolException(Format(Strings.NoSuchTab, id.Trim(), Ids()))
            : Current ?? (opens ? Open() : _tabs.Active ?? throw new AgentToolException(Strings.NoPage));
        (_target, _opened) = (tab, null);
        return tab;
    }

    /// <summary>A new tab of the agent's; it is shown at once only if that hides no page of the user's.</summary>
    public BrowserTab Open()
    {
        var tab = _tabs.Open(BrowserTabOwner.Agent, select: _tabs.Active is null or { IsAgent: true } or { HasWebPage: false });
        (_current, _target, _opened) = (tab, tab, null);
        return tab;
    }

    /// <summary>The tab the last call's action opened, once: the agent continues there.</summary>
    public BrowserTab? TakeOpened()
    {
        var opened = _opened;
        _opened = null;
        return opened is not null && _tabs.Tabs.Contains(opened) ? opened : null;
    }

    /// <summary>
    /// Remembers which refs of the tab's latest snapshot are links: following a link is reading on, pressing anything
    /// else is acting on the site, and the approval is decided before the page can be asked.
    /// </summary>
    public void RememberLinks(BrowserTab tab, string snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _links.AddOrUpdate(tab, [.. LinkRef().Matches(snapshot).Select(static match => match.Groups[1].Value)]);
    }

    public bool IsLink(BrowserTab tab, string? reference) =>
        reference is not null && _links.TryGetValue(tab, out var links) && links.Contains(reference.Trim());

    /// <summary>Shows the tab to the user, bringing the panel to the front.</summary>
    public async Task SelectAsync(BrowserTab tab)
    {
        _tabs.Select(tab);
        if (_showPanel is not null)
        {
            await _showPanel();
        }
    }

    public void Close(BrowserTab tab) => _tabs.Close(tab);

    /// <summary>Every tab with its full address, for the <c>tabs</c> action.</summary>
    public string Describe()
    {
        var active = _tabs.Active;
        var tabs = _tabs.Tabs.Select(tab => new BrowserTabInfo(tab.Id, tab.Page.Url ?? Blank, tab.Page.Title, tab == active, tab.IsAgent)).ToList();
        return tabs.Count == 0 ? Strings.NoTabs : string.Join('\n', BrowserTabLines.List(tabs, Current?.Id));
    }

    public void Dispose() => _tabs.Opened -= OnOpened;

    // A window opened by the tab the agent just acted in: the agent's own tab hands over to it.
    private void OnOpened(object? sender, BrowserTabEventArgs e)
    {
        if (e.Tab.OpenerId is not { } opener || opener != _target?.Id)
        {
            return;
        }

        _opened = e.Tab;
        if (e.Tab.IsAgent && opener == _current?.Id)
        {
            _current = e.Tab;
        }
    }

    [GeneratedRegex(@"(?:^|[\s|])link ""[^\n]*?\[ref=(e\d+)\]", RegexOptions.Multiline)]
    private static partial Regex LinkRef();

    private string Ids() => _tabs.Tabs is { IsEmpty: false } tabs ? string.Join(", ", tabs.Select(static tab => tab.Id)) : "—";

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
