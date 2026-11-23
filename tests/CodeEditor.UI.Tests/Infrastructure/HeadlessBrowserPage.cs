using System.Drawing;
using System.Windows.Interop;
using System.Windows.Threading;
using CodeEditor.Modules.Browser.Services;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// A real WebView2 page nobody sees, for testing the browser tool's page scripts on the real engine with no mouse or
/// keyboard: the controller lives in a popup window far off the screen (a hidden one would not render, and a
/// screenshot needs a frame) on its own UI thread. Implements the part of
/// <see cref="IBrowserPage"/> the tool's reading and acting use.
/// </summary>
internal sealed class HeadlessBrowserPage : IBrowserPage
{
    private const int Width = 1200;
    private const int Height = 900;
    private const int WindowPopup = unchecked((int)0x80000000);
    private const int WindowVisible = 0x10000000;
    private const int OffScreen = -20_000;
    private const int WindowExToolWindow = 0x00000080;
    private const int WindowExNoActivate = 0x08000000;

    private readonly Dispatcher _dispatcher;
    private readonly string _data = Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", "webview-" + Guid.NewGuid().ToString("N"));
    private HwndSource? _window;
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _core;

    public HeadlessBrowserPage()
    {
        var started = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() =>
        {
            started.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        _dispatcher = started.Task.GetAwaiter().GetResult();
    }

    public Uri? Url { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public bool CanGoBack => false;

    public bool CanGoForward => false;

    public bool IsLoading { get; private set; }

    public int? StatusCode { get; private set; }

    public ReadOnlyMemory<byte> Favicon => default;

    public BrowserNetworkLog Network { get; } = new();

    /// <summary>Dialogs the page opened (<c>Page.javascriptDialogOpening</c> parameters), oldest first.</summary>
    public List<string> Dialogs { get; } = [];

    public event EventHandler? StateChanged;

#pragma warning disable CS0067 // The headless page opens no windows and never closes itself.
    public event EventHandler<BrowserPopupEventArgs>? PopupRequested;

    public event EventHandler? CloseRequested;
#pragma warning restore CS0067

    public async Task NavigateAsync(Uri url, CancellationToken cancellationToken) => await await _dispatcher.InvokeAsync(async () =>
    {
        var core = _core ??= await CreateAsync();
        var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            core.NavigationCompleted -= OnCompleted;
            StatusCode = e.HttpStatusCode > 0 ? e.HttpStatusCode : null;
            loaded.TrySetResult(e.IsSuccess);
        }

        core.NavigationCompleted += OnCompleted;
        IsLoading = true;
        core.Navigate(url.AbsoluteUri);
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        IsLoading = false;
        Refresh(core);
    });

    public Task GoBackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task GoForwardAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ReloadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task WaitForLoadAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
        await _dispatcher.InvokeAsync(() => Refresh(_core!));
    }

    public async Task<string> EvaluateAsync(string script, CancellationToken cancellationToken) =>
        await (await _dispatcher.InvokeAsync(() => _core!.ExecuteScriptAsync(script))).WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);

    public async Task<string> DevToolsAsync(string method, string parameters, CancellationToken cancellationToken) =>
        await (await _dispatcher.InvokeAsync(() => _core!.CallDevToolsProtocolMethodAsync(method, parameters))).WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);

    public async Task<byte[]> CaptureAsync(BrowserImageFormat format, CancellationToken cancellationToken)
    {
        var metrics = await DevToolsAsync("Page.getLayoutMetrics", "{}", cancellationToken);
        return BrowserScreenshot.Data(await DevToolsAsync("Page.captureScreenshot", BrowserScreenshot.Request(metrics, format), cancellationToken));
    }

    public IReadOnlyList<BrowserConsoleMessage> TakeConsole() => [];

    public void Dispose()
    {
        _dispatcher.Invoke(() =>
        {
            _controller?.Close();
            _window?.Dispose();
        });
        _dispatcher.InvokeShutdown();
        AppSession.DeleteQuietly(_data);
    }

    private async Task<CoreWebView2> CreateAsync()
    {
        _window = new HwndSource(new HwndSourceParameters("CodeEditor.UI.Tests.Page", Width, Height)
        {
            WindowStyle = WindowPopup | WindowVisible,
            ExtendedWindowStyle = WindowExToolWindow | WindowExNoActivate,
            PositionX = OffScreen,
            PositionY = OffScreen,
        });
        var environment = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: _data);
        _controller = await environment.CreateCoreWebView2ControllerAsync(_window.Handle);
        _controller.Bounds = new Rectangle(0, 0, Width, Height);
        var core = _controller.CoreWebView2;
        foreach (var domain in (string[])["Runtime.enable", "Page.enable", "Network.enable"])
        {
            await core.CallDevToolsProtocolMethodAsync(domain, "{}");
        }

        core.GetDevToolsProtocolEventReceiver("Page.javascriptDialogOpening").DevToolsProtocolEventReceived += (_, e) => Dialogs.Add(e.ParameterObjectAsJson);
        foreach (var method in BrowserNetworkLog.Methods)
        {
            core.GetDevToolsProtocolEventReceiver(method).DevToolsProtocolEventReceived += (_, e) => Network.Record(method, e.ParameterObjectAsJson);
        }

        return core;
    }

    private void Refresh(CoreWebView2 core)
    {
        Url = Uri.TryCreate(core.Source, UriKind.Absolute, out var url) ? url : null;
        Title = core.DocumentTitle ?? string.Empty;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
