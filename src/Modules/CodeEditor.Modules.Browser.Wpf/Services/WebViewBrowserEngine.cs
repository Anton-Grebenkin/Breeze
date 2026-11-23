using CodeEditor.Core.Storage;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Shell.Layout;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Browser.Wpf.Services;

/// <summary>
/// WebView2 browser (ADR 0027, 0054): makes the pages of the tabs (<see cref="WebViewPage"/>). Every page shares one
/// environment, so browser data — cookies, cache, site storage — is one profile in the editor's data folder and
/// survives restarts. The panel hosts the controls of the pages that were shown or called.
/// </summary>
public sealed class WebViewBrowserEngine : IBrowserEngine, IDisposable
{
    public const string DataFolderName = "WebView2";

    private readonly WebViewHost _host;
    private readonly Lock _gate = new();
    private readonly List<WebViewPage> _pages = [];

    public WebViewBrowserEngine(IUiDispatcher dispatcher, WorkbenchLayout layout, UserDataPaths paths, ILogger<WebViewBrowserEngine> logger)
    {
        _host = new WebViewHost(dispatcher, layout, paths, logger);
        _host.PagesChanged += (_, _) => PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A page was made or closed, or must now be hosted by the panel; raised on any thread.</summary>
    public event EventHandler? PagesChanged;

    public IReadOnlyList<WebViewPage> Pages
    {
        get
        {
            lock (_gate)
            {
                return [.. _pages];
            }
        }
    }

    public IBrowserPage CreatePage(BrowserPageState? restored = null)
    {
        var page = new WebViewPage(_host, restored, Remove);
        lock (_gate)
        {
            _pages.Add(page);
        }

        _host.NotifyPagesChanged();
        return page;
    }

    public void Dispose()
    {
        foreach (var page in Pages)
        {
            page.Dispose();
        }
    }

    private void Remove(WebViewPage page)
    {
        lock (_gate)
        {
            _pages.Remove(page);
        }

        _host.NotifyPagesChanged();
    }
}
