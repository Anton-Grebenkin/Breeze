using System.Globalization;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Browser.Services.Tabs;

/// <summary>
/// A page in a tab of the agent's needs a person — the browser shows its own sign-in window, which only the user can
/// answer. The tab works behind the user's pages, so nobody would see that window: the tab is shown, the panel comes
/// to the front and a notification says what is asked. Once per window.
/// </summary>
public sealed class BrowserAttention : IDisposable
{
    public const string NotificationId = "browser.attention";

    private readonly BrowserTabs _tabs;
    private readonly INotificationService _notifications;
    private readonly Func<Task>? _showPanel;
    private readonly HashSet<BrowserTab> _asked = [];

    /// <param name="showPanel">Brings the browser panel to the front without focus; <c>null</c> in tests.</param>
    public BrowserAttention(BrowserTabs tabs, INotificationService notifications, Func<Task>? showPanel = null)
    {
        _tabs = tabs;
        _notifications = notifications;
        _showPanel = showPanel;
        _tabs.PageChanged += OnPageChanged;
    }

    public void Dispose() => _tabs.PageChanged -= OnPageChanged;

    private void OnPageChanged(object? sender, BrowserTabEventArgs e)
    {
        lock (_asked)
        {
            if (!e.Tab.Page.NeedsUser)
            {
                _asked.Remove(e.Tab);
                return;
            }

            if (!e.Tab.IsAgent || !_asked.Add(e.Tab))
            {
                return;
            }
        }

        _tabs.Select(e.Tab);
        _ = _showPanel?.Invoke();
        _ = _notifications.Show(new Notification(NotificationId, string.Format(CultureInfo.CurrentCulture, Strings.SignInWindowNotification, e.Tab.Page.Url?.IdnHost ?? "?"), []));
    }
}
