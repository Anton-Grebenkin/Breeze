using System.Globalization;
using System.Runtime.InteropServices;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CodeEditor.Modules.Browser.Wpf.Services;

/// <summary>
/// A browser page on WebView2 (ADR 0027, 0054): one control per tab (<see cref="Control"/>), created when the panel
/// first hosts it. A page kept from the last run shows its address at once and loads when first shown or called. A call
/// before the panel hosts the page asks for that and waits until the browser is ready; WebView2 is used only on the UI
/// thread, so calls are marshaled there. Console messages, page errors and network requests arrive as DevTools events.
/// </summary>
public sealed partial class WebViewPage : IBrowserPage
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(30);

    // A page script never takes this long unless a dialog (alert, confirm) holds the page.
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromSeconds(20);

    // Navigation after a click does not start at once; this is how long we wait for it to begin.
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(400);

    private readonly WebViewHost _host;
    private readonly Action<WebViewPage> _closed;
    private readonly BrowserConsoleLog _console = new();
    private TaskCompletionSource<CoreWebView2> _ready = NewReady();
    private WebView2? _control;

    // The awaited load: set and cleared on the UI thread, read by the post-click wait on the tool's thread.
    private volatile TaskCompletionSource<string?>? _loading;

    // The address kept from the last run, opened once the browser is ready; calls wait for that load.
    private Uri? _pending;
    private volatile Task? _restoring;
    private volatile bool _disposed;

    internal WebViewPage(WebViewHost host, BrowserPageState? restored, Action<WebViewPage> closed)
    {
        _host = host;
        _closed = closed;
        if (restored is not null)
        {
            (Url, Title, _pending) = (restored.Url, restored.Title, restored.Url);
        }
    }

    public Uri? Url { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public bool CanGoBack { get; private set; }

    public bool CanGoForward { get; private set; }

    public bool IsLoading { get; private set; }

    public int? StatusCode { get; private set; }

    public ReadOnlyMemory<byte> Favicon { get; private set; }

    public BrowserNetworkLog Network { get; } = new();

    public event EventHandler? StateChanged;

    public event EventHandler<BrowserPopupEventArgs>? PopupRequested;

    public event EventHandler? CloseRequested;

    /// <summary>The page was called before the panel ever showed it: the panel hosts its control now.</summary>
    public bool IsWanted { get; private set; }

    public bool HasControl => _control is not null;

    /// <summary>The browser control for the panel, created on first use. UI thread only.</summary>
    public WebView2 Control => _control ??= Create();

    public async Task NavigateAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        await LoadAsync(core => core.Navigate(url.AbsoluteUri), cancellationToken);
    }

    public Task GoBackAsync(CancellationToken cancellationToken) => LoadAsync(core => core.GoBack(), cancellationToken, () => CanGoBack);

    public Task GoForwardAsync(CancellationToken cancellationToken) => LoadAsync(core => core.GoForward(), cancellationToken, () => CanGoForward);

    public Task ReloadAsync(CancellationToken cancellationToken) => LoadAsync(core => core.Reload(), cancellationToken);

    public async Task WaitForLoadAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        await Task.Delay(SettleDelay, cancellationToken);
        if (_loading is { } loading)
        {
            // A failed navigation after a click shows up in the page snapshot; the step is not aborted.
            await ((Task)loading.Task.WaitAsync(timeout, cancellationToken)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public async Task<string> EvaluateAsync(string script, CancellationToken cancellationToken)
    {
        var core = await PageAsync(show: false, cancellationToken);
        Task<string>? pending = null;
        await _host.Dispatcher.InvokeAsync(() => pending = Awake(core).ExecuteScriptAsync(script));
        return await WithinScriptTimeoutAsync(pending!, cancellationToken);
    }

    // Only a visible browser renders a screenshot: the panel is shown without focus, as on navigation.
    public async Task<byte[]> CaptureAsync(BrowserImageFormat format, CancellationToken cancellationToken)
    {
        var core = await PageAsync(show: true, cancellationToken);
        try
        {
            var metrics = await CallAsync(core, "Page.getLayoutMetrics", "{}", cancellationToken);
            return BrowserScreenshot.Data(await CallAsync(core, "Page.captureScreenshot", BrowserScreenshot.Request(metrics, format), cancellationToken));
        }
        catch (BrowserException exception)
        {
            throw new BrowserException(Strings.ScreenshotFailed, exception);
        }
    }

    public async Task<string> DevToolsAsync(string method, string parameters, CancellationToken cancellationToken) =>
        await CallAsync(await PageAsync(show: false, cancellationToken), method, parameters, cancellationToken);

    public IReadOnlyList<BrowserConsoleMessage> TakeConsole() => _console.Take();

    // Navigates and waits for the load; the panel comes to the front because the agent looks at the page.
    private async Task LoadAsync(Action<CoreWebView2> start, CancellationToken cancellationToken, Func<bool>? possible = null)
    {
        _pending = null;
        var core = await ReadyAsync(show: true, cancellationToken);
        TaskCompletionSource<string?>? loading = null;
        await _host.Dispatcher.InvokeAsync(() =>
        {
            if (possible?.Invoke() == false)
            {
                return;
            }

            loading = _loading = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            start(Awake(core));
        });
        if (loading is null)
        {
            return;
        }

        string? error;
        try
        {
            error = await loading.Task.WaitAsync(LoadTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new BrowserException(Format(Strings.LoadTimedOut, (int)LoadTimeout.TotalSeconds));
        }

        if (error is not null)
        {
            throw new BrowserException(Format(Strings.NavigationFailed, error));
        }
    }

    private async Task<string> CallAsync(CoreWebView2 core, string method, string parameters, CancellationToken cancellationToken)
    {
        Task<string>? pending = null;
        await _host.Dispatcher.InvokeAsync(() => pending = Awake(core).CallDevToolsProtocolMethodAsync(method, parameters));
        try
        {
            return await WithinScriptTimeoutAsync(pending!, cancellationToken);
        }
        catch (Exception exception) when (exception is COMException or ArgumentException)
        {
            throw new BrowserException(Format(Strings.DevToolsFailed, method, exception.Message), exception);
        }
    }

    // A dialog (alert, confirm) holds every script until it closes: the agent is told why instead of waiting forever.
    private static async Task<string> WithinScriptTimeoutAsync(Task<string> pending, CancellationToken cancellationToken)
    {
        try
        {
            return await pending.WaitAsync(ScriptTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new BrowserException(Format(Strings.ScriptTimedOut, (int)ScriptTimeout.TotalSeconds));
        }
    }

    private async Task<CoreWebView2> PageAsync(bool show, CancellationToken cancellationToken)
    {
        var core = await ReadyAsync(show, cancellationToken);
        if (_restoring is { } restoring)
        {
            await restoring.WaitAsync(LoadTimeout, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return Url is { Scheme: "http" or "https" } ? core : throw new BrowserException(Strings.NoPage);
    }

    private async Task<CoreWebView2> ReadyAsync(bool show, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new BrowserException(Strings.TabClosed);
        }

        var ready = _ready.Task;
        if (!ready.IsCompleted && !IsWanted)
        {
            IsWanted = true;
            _host.NotifyPagesChanged();
        }

        if (show || !ready.IsCompleted)
        {
            await _host.ShowPanelAsync();
        }

        try
        {
            return await ready.WaitAsync(ReadyTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new BrowserException(Format(Strings.BrowserUnavailable, Format(Strings.LoadTimedOut, (int)ReadyTimeout.TotalSeconds)));
        }
    }

    private static TaskCompletionSource<CoreWebView2> NewReady() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
