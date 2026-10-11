using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using CodeEditor.Core.Storage;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Shell.Layout;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CodeEditor.Modules.Browser.Wpf.Services;

/// <summary>
/// WebView2 browser (ADR 0027). One control for the app lifetime (<see cref="Control"/>): the panel only hosts it, so
/// switching bottom panel tabs keeps the page. An agent call before the first panel show shows it and waits until ready;
/// the panel is shown without focus so the chat box keeps it. WebView2 is used only on the UI thread, so calls are
/// marshaled there. Console messages, page errors and network requests arrive as DevTools events
/// (<see cref="BrowserConsoleLog"/>, <see cref="BrowserNetworkLog"/>). Browser data — cookies, cache, site storage —
/// lives in the editor's data folder and survives restarts.
/// </summary>
public sealed partial class WebViewBrowserEngine(IUiDispatcher dispatcher, WorkbenchLayout layout, UserDataPaths paths, BrowserNetworkLog network, ILogger<WebViewBrowserEngine> logger)
    : IBrowserEngine, IDisposable
{
    public const string DataFolderName = "WebView2";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(30);

    // A page script never takes this long unless a dialog (alert, confirm) holds the page.
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromSeconds(20);

    // Navigation after a click does not start at once; this is how long we wait for it to begin.
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(400);

    private readonly BrowserConsoleLog _console = new();
    private readonly TaskCompletionSource<CoreWebView2> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebView2? _control;

    // The awaited load: set and cleared on the UI thread, read by the post-click wait on the tool's thread.
    private volatile TaskCompletionSource<string?>? _loading;

    public Uri? Url { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public bool CanGoBack { get; private set; }

    public bool CanGoForward { get; private set; }

    public bool IsLoading { get; private set; }

    public int? StatusCode { get; private set; }

    public event EventHandler? StateChanged;

    /// <summary>The browser control for the panel, created on first panel show. UI thread only.</summary>
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
        await dispatcher.InvokeAsync(() => pending = core.ExecuteScriptAsync(script));
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

    public void Dispose() => _control?.Dispose();

    // Navigates and waits for the load; the panel comes to the front because the agent looks at the page.
    private async Task LoadAsync(Action<CoreWebView2> start, CancellationToken cancellationToken, Func<bool>? possible = null)
    {
        var core = await ReadyAsync(show: true, cancellationToken);
        TaskCompletionSource<string?>? loading = null;
        await dispatcher.InvokeAsync(() =>
        {
            if (possible?.Invoke() == false)
            {
                return;
            }

            loading = _loading = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            start(core);
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
        await dispatcher.InvokeAsync(() => pending = core.CallDevToolsProtocolMethodAsync(method, parameters));
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
        return Url is { Scheme: "http" or "https" } ? core : throw new BrowserException(Strings.NoPage);
    }

    private async Task<CoreWebView2> ReadyAsync(bool show, CancellationToken cancellationToken)
    {
        if (show || !_ready.Task.IsCompleted)
        {
            await dispatcher.InvokeAsync(() => layout.Show(BrowserModule.ToolWindowId, focus: false));
        }

        try
        {
            return await _ready.Task.WaitAsync(ReadyTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new BrowserException(Format(Strings.BrowserUnavailable, Format(Strings.LoadTimedOut, (int)ReadyTimeout.TotalSeconds)));
        }
    }

    private WebView2 Create()
    {
        var control = new WebView2();
        AutomationProperties.SetAutomationId(control, "Browser.Page");
        _ = InitializeAsync(control);
        return control;
    }

    private async Task InitializeAsync(WebView2 control)
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: paths.File(DataFolderName));
            await control.EnsureCoreWebView2Async(environment);
            var core = control.CoreWebView2;
            Subscribe(core);
            foreach (var domain in (string[])["Runtime.enable", "Log.enable", "Network.enable"])
            {
                await core.CallDevToolsProtocolMethodAsync(domain, "{}");
            }

            _ready.TrySetResult(core);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogUnavailable(logger, exception);
            _ready.TrySetException(new BrowserException(Format(Strings.BrowserUnavailable, exception.Message), exception));
        }
    }

    private void Subscribe(CoreWebView2 core)
    {
        core.NavigationStarting += (_, _) =>
        {
            _loading ??= new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            IsLoading = true;
            Update(core);
        };

        // A server's error page (404, 500) loads like any page: the agent reads it, with its status.
        core.NavigationCompleted += (_, e) =>
        {
            StatusCode = e.HttpStatusCode > 0 ? e.HttpStatusCode : null;
            Finish(core, e.IsSuccess || e.HttpStatusCode > 0 ? null : BrowserErrors.Describe(e.WebErrorStatus.ToString()));
        };

        // In-page navigation (anchor, history.pushState) starts no load, so nothing needs to wait for one.
        core.SourceChanged += (_, e) =>
        {
            if (!e.IsNewDocument && _loading is not null && !IsLoading)
            {
                Finish(core, error: null);
            }

            Update(core);
        };
        core.DocumentTitleChanged += (_, _) => Update(core);
        core.HistoryChanged += (_, _) => Update(core);

        // A new window opens in the same panel, so the agent and the user look at one page.
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            core.Navigate(e.Uri);
        };
        foreach (var method in BrowserConsoleEvents.Methods)
        {
            core.GetDevToolsProtocolEventReceiver(method).DevToolsProtocolEventReceived += (_, e) => _console.Record(method, e.ParameterObjectAsJson);
        }

        foreach (var method in BrowserNetworkLog.Methods)
        {
            core.GetDevToolsProtocolEventReceiver(method).DevToolsProtocolEventReceived += (_, e) => network.Record(method, e.ParameterObjectAsJson);
        }
    }

    private void Finish(CoreWebView2 core, string? error)
    {
        IsLoading = false;
        var loading = _loading;
        _loading = null;
        loading?.TrySetResult(error);
        Update(core);
    }

    private void Update(CoreWebView2 core)
    {
        Url = Uri.TryCreate(core.Source, UriKind.Absolute, out var url) ? url : null;
        Title = core.DocumentTitle ?? string.Empty;
        CanGoBack = core.CanGoBack;
        CanGoForward = core.CanGoForward;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The browser (WebView2) is not available")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}
