using System.Runtime.InteropServices;
using System.Windows.Automation;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.ViewModels;
using CodeEditor.Modules.Terminal.Wpf.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CodeEditor.Modules.Terminal.Wpf.Views;

/// <summary>
/// The <c>terminal.html</c> page: a WebView2 control that exchanges <see cref="TerminalPageMessages"/>. Nothing is sent
/// before the page is ready; then the panel sends the whole state. Browser keys, menus and zoom are off, so workbench
/// keys reach the window and the rest goes to the shell.
/// </summary>
internal sealed partial class TerminalPage(TerminalWebEnvironment environment, ILogger logger) : IDisposable
{
    private bool _ready;
    private bool _disposed;

    public WebView2 Control { get; } = CreateControl();

    /// <summary>The page has loaded and takes messages.</summary>
    public bool IsReady => _ready && !_disposed;

    /// <summary>A page message; <c>ready</c> arrives after each load and reload.</summary>
    public event EventHandler<TerminalPageMessage>? MessageReceived;

    /// <returns><c>false</c> if WebView2 is not available.</returns>
    public async Task<bool> OpenAsync(System.Drawing.Color background)
    {
        Control.DefaultBackgroundColor = background;
        try
        {
            await Control.EnsureCoreWebView2Async(await environment.GetAsync());
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or COMException or InvalidOperationException or ArgumentException or ObjectDisposedException)
        {
            LogUnavailable(logger, exception);
            return false;
        }

        if (_disposed)
        {
            return false;
        }

        var core = Control.CoreWebView2;
        Configure(core.Settings);
        TerminalAssets.Map(core);
        core.NavigationStarting += (_, e) => e.Cancel = !TerminalAssets.IsOwn(e.Uri);
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.WebMessageReceived += OnWebMessageReceived;
        core.Navigate(TerminalAssets.Page.AbsoluteUri);
        return true;
    }

    /// <summary>
    /// Sends the message after the current handler: WebView2 cannot be called while it waits for a key handler, and
    /// output may arrive then. Message order is preserved.
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
        if (TerminalPageMessages.Read(e.WebMessageAsJson) is not { } message)
        {
            return;
        }

        _ready |= message.Type == TerminalPageMessages.Ready;
        MessageReceived?.Invoke(this, message);
    }

    private static WebView2 CreateControl()
    {
        var control = new WebView2 { Focusable = true };
        AutomationProperties.SetAutomationId(control, "Terminal.Page");
        AutomationProperties.SetName(control, Strings.ModuleName);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "The terminal page (WebView2) is not available")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}
