using System.Runtime.InteropServices;
using System.Windows.Automation;
using CodeEditor.Modules.Viewers.Services;
using CodeEditor.Modules.Viewers.Wpf.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>
/// The page of an SVG or media tab: a WebView2 control in the shared viewer environment, exchanging messages
/// (<see cref="ViewerPageMessages"/>). Nothing is sent before the page is ready; once ready, the tab sends a full show.
/// The page never navigates elsewhere, opens windows or downloads anything; the browser menu, zoom and accelerator keys
/// are off so editor keys reach the window.
/// </summary>
internal sealed partial class ViewerPage : IDisposable
{
    private readonly ViewerWebEnvironment _environment;
    private readonly ILogger _logger;
    private bool _ready;
    private bool _disposed;

    public ViewerPage(ViewerWebEnvironment environment, ILogger logger, string automationId, string name)
    {
        _environment = environment;
        _logger = logger;
        Control = new WebView2 { Focusable = true };
        AutomationProperties.SetAutomationId(Control, automationId);
        AutomationProperties.SetName(Control, name);
    }

    public WebView2 Control { get; }

    /// <summary>A page message; <c>ready</c> comes after every load and reload.</summary>
    public event EventHandler<ViewerPageMessage>? MessageReceived;

    /// <summary>WebView2 is unavailable (no WebView2 Runtime); the argument is the reason for the user.</summary>
    public event EventHandler<string>? Unavailable;

    /// <param name="page">A page from the <c>Viewers</c> folder: <see cref="ViewerAddresses.SvgPage"/> or <see cref="ViewerAddresses.MediaPage"/>.</param>
    /// <param name="fileFolder">The shown file's folder, which the virtual host exposes to the page.</param>
    public async Task OpenAsync(string page, string fileFolder, System.Drawing.Color background)
    {
        Control.DefaultBackgroundColor = background;
        try
        {
            await Control.EnsureCoreWebView2Async(await _environment.GetAsync());
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or COMException or InvalidOperationException or ArgumentException or ObjectDisposedException)
        {
            LogUnavailable(_logger, exception);
            if (!_disposed)
            {
                Unavailable?.Invoke(this, exception.Message);
            }

            return;
        }

        if (_disposed)
        {
            return;
        }

        var core = Control.CoreWebView2;
        Configure(core.Settings);
        ViewerAssets.Map(core, fileFolder);
        core.NavigationStarting += (_, e) => e.Cancel = !ViewerAddresses.IsPage(e.Uri);
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.WebMessageReceived += OnWebMessageReceived;
        core.Navigate(ViewerAddresses.Page(page).AbsoluteUri);
    }

    /// <summary>
    /// Sends the message after the current handler: the command may have come from a key pressed in the page itself, and
    /// WebView2 must not be called while it waits for its key handler. Message order is preserved.
    /// </summary>
    public void Post(string message) => Control.Dispatcher.BeginInvoke(() =>
    {
        if (_ready && !_disposed)
        {
            Control.CoreWebView2?.PostWebMessageAsJson(message);
        }
    });

    public void Dispose()
    {
        _disposed = true;
        Control.Dispose();
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (ViewerPageMessages.Read(e.WebMessageAsJson) is not { } message)
        {
            return;
        }

        _ready |= message.Type == ViewerPageMessages.Ready;
        MessageReceived?.Invoke(this, message);
    }

    private static void Configure(CoreWebView2Settings settings)
    {
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPinchZoomEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsWebMessageEnabled = true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The file viewer page (WebView2) is not available")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}
