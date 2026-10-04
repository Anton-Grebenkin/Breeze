using System.Runtime.InteropServices;
using System.Windows.Automation;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.ViewModels;
using CodeEditor.Modules.Diagrams.Wpf.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CodeEditor.Modules.Diagrams.Wpf.Views;

/// <summary>
/// The <c>preview.html</c> page in the preview tab: a WebView2 control in the shared diagram environment that exchanges
/// messages (<see cref="DiagramPageMessages"/>). Nothing is sent before the page is ready; once it is, the tab sends the
/// whole state. The page never navigates or opens windows; the browser menu, zoom and keys are disabled so editor keys
/// reach the window.
/// </summary>
internal sealed partial class PreviewPage(DiagramWebEnvironment environment, ILogger logger) : IDisposable
{
    private bool _ready;
    private bool _disposed;

    public WebView2 Control { get; } = CreateControl();

    /// <summary>A page message; <c>ready</c> arrives after each load and reload.</summary>
    public event EventHandler<DiagramPageMessage>? MessageReceived;

    public async Task OpenAsync(System.Drawing.Color background)
    {
        Control.DefaultBackgroundColor = background;
        try
        {
            await Control.EnsureCoreWebView2Async(await environment.GetAsync());
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or COMException or InvalidOperationException or ArgumentException or ObjectDisposedException)
        {
            // The error strip shows the reason: the renderer on the same environment reports it when rendering.
            LogUnavailable(logger, exception);
            return;
        }

        if (_disposed)
        {
            return;
        }

        var core = Control.CoreWebView2;
        Configure(core.Settings);
        DiagramAssets.Map(core);
        core.NavigationStarting += (_, e) => e.Cancel = !DiagramAssets.IsOwn(e.Uri);
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.WebMessageReceived += OnWebMessageReceived;
        core.Navigate(DiagramAssets.Preview.AbsoluteUri);
    }

    /// <summary>
    /// Sends the message after the current handler: the command may come from a key in the page itself, and WebView2
    /// cannot be called while it waits for a key handler. Message order is preserved.
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
        if (DiagramPageMessages.Read(e.WebMessageAsJson) is not { } message)
        {
            return;
        }

        _ready |= message.Type == DiagramPageMessages.Ready;
        MessageReceived?.Invoke(this, message);
    }

    private static WebView2 CreateControl()
    {
        var control = new WebView2 { Focusable = true };
        AutomationProperties.SetAutomationId(control, "DiagramPreview.Page");
        AutomationProperties.SetName(control, Strings.PreviewPage);
        return control;
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "The diagram preview page (WebView2) is not available")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}
