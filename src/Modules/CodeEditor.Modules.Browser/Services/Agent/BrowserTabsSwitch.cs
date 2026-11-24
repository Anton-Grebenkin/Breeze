using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Shell;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Browser.Services.Agent;

/// <summary>
/// The "browser tabs" chip above the chat input: whether the agent is told which sites are open. The choice is the
/// user's <c>browser.shareTabsWithAgent</c> setting, so it survives a restart.
/// </summary>
public sealed partial class BrowserTabsSwitch : IAgentContextSwitch, IDisposable
{
    public const string SwitchId = "BrowserTabs";
    public const string ShareKey = BrowserOptions.Section + ".shareTabsWithAgent";

    private readonly BrowserTabs _tabs;
    private readonly IOptionsMonitor<BrowserOptions> _options;
    private readonly ISettingsService _settings;
    private readonly ILogger<BrowserTabsSwitch> _logger;
    private readonly IDisposable? _subscription;

    public BrowserTabsSwitch(BrowserTabs tabs, IOptionsMonitor<BrowserOptions> options, ISettingsService settings, ILogger<BrowserTabsSwitch> logger)
    {
        _tabs = tabs;
        _options = options;
        _settings = settings;
        _logger = logger;
        _tabs.Changed += OnTabsChanged;
        _tabs.PageChanged += OnTabsChanged;
        _subscription = options.OnChange(_ => Notify());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id => SwitchId;

    public string Icon => IconNames.Browser;

    public string Label => Tabs() is [var only] ? only.Url.IdnHost : string.Format(CultureInfo.CurrentCulture, Strings.TabsChip, Tabs().Count);

    public string ToolTip => IsOn ? Strings.TabsShared : Strings.TabsNotShared;

    public bool IsAvailable => Tabs().Count > 0;

    public bool IsOn
    {
        get => _options.CurrentValue.ShareTabsWithAgent;
        set
        {
            if (value != IsOn && !_settings.TrySetUserValue(ShareKey, value, out var error))
            {
                LogNotSaved(_logger, error);
            }
        }
    }

    /// <summary>Open http(s) pages, as the agent would be told about them.</summary>
    public IReadOnlyList<BrowserTabInfo> Tabs()
    {
        var active = _tabs.Active;
        return [.. _tabs.Tabs.Where(static tab => tab.HasWebPage).Select(tab => new BrowserTabInfo(tab.Id, tab.Page.Url!, tab.Page.Title, tab == active, tab.IsAgent))];
    }

    public void Toggle() => IsOn = !IsOn;

    public void Dispose()
    {
        _tabs.Changed -= OnTabsChanged;
        _tabs.PageChanged -= OnTabsChanged;
        _subscription?.Dispose();
    }

    private void OnTabsChanged(object? sender, EventArgs e) => Notify();

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    [LoggerMessage(Level = LogLevel.Warning, Message = "The browser tabs switch was not saved: {Error}")]
    private static partial void LogNotSaved(ILogger logger, string error);
}
