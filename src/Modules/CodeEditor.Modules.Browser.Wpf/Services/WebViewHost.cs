using CodeEditor.Core.Storage;
using CodeEditor.Core.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Browser.Wpf.Services;

/// <summary>
/// What every browser page shares: one WebView2 environment, so tabs live in one profile (cookies, sign-ins, cache) in
/// the editor's data folder, plus the UI thread and the panel that shows the pages.
/// </summary>
internal sealed class WebViewHost(IUiDispatcher dispatcher, Action showPanel, UserDataPaths paths, ILogger logger)
{
    private Task<CoreWebView2Environment>? _environment;

    /// <summary>A page was made or closed, got a new control, or must now be hosted by the panel.</summary>
    public event EventHandler? PagesChanged;

    public IUiDispatcher Dispatcher => dispatcher;

    public ILogger Logger => logger;

    /// <summary>UI thread only.</summary>
    public Task<CoreWebView2Environment> EnvironmentAsync() =>
        _environment ??= CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: paths.File(WebViewBrowserEngine.DataFolderName));

    /// <summary>The browser process is gone: the next page starts a new one.</summary>
    public void ForgetEnvironment() => _environment = null;

    /// <summary>The panel comes to the front without focus, so the chat box keeps it.</summary>
    public Task ShowPanelAsync() => dispatcher.InvokeAsync(showPanel);

    public void NotifyPagesChanged() => PagesChanged?.Invoke(this, EventArgs.Empty);
}
