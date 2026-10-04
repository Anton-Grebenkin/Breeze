using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Diagrams.Wpf.Services;

/// <summary>
/// A WebView2 page without a visible window: the controller lives in a hidden popup window that is neither on screen
/// nor in the taskbar. Mermaid needs a real document (it measures text through layout), but there is no need to show
/// it. One page, one open: a closed page is replaced by a new one. UI thread only.
/// </summary>
internal sealed class HiddenWebPage : IDisposable
{
    // Page size: Mermaid lays the diagram out by its own sizes; the window only sets the viewport.
    private const int Width = 1600;
    private const int Height = 1200;

    private const int WindowPopup = unchecked((int)0x80000000);
    private const int WindowExToolWindow = 0x00000080;
    private const int WindowExNoActivate = 0x08000000;

    private HwndSource? _window;
    private CoreWebView2Controller? _controller;
    private bool _disposed;

    /// <summary>Creates the window and opens the page.</summary>
    /// <exception cref="DiagramRendererException">The page did not open.</exception>
    /// <exception cref="ObjectDisposedException">The page was closed while opening.</exception>
    public async Task<CoreWebView2> OpenAsync(CoreWebView2Environment environment, Uri page)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _window = new HwndSource(new HwndSourceParameters("CodeEditor.Diagrams.Renderer", Width, Height)
        {
            WindowStyle = WindowPopup,
            ExtendedWindowStyle = WindowExToolWindow | WindowExNoActivate,
        });
        var controller = await environment.CreateCoreWebView2ControllerAsync(_window.Handle);
        if (_disposed)
        {
            controller.Close();
            throw new ObjectDisposedException(nameof(HiddenWebPage));
        }

        _controller = controller;
        _controller.Bounds = new Rectangle(0, 0, Width, Height);
        var core = _controller.CoreWebView2;
        Configure(core.Settings);
        DiagramAssets.Map(core);
        core.NavigationStarting += (_, e) => e.Cancel = !DiagramAssets.IsOwn(e.Uri);
        core.NewWindowRequested += (_, e) => e.Handled = true;

        var loaded = new TaskCompletionSource<CoreWebView2WebErrorStatus?>(TaskCreationOptions.RunContinuationsAsynchronously);
        core.NavigationCompleted += (_, e) => loaded.TrySetResult(e.IsSuccess ? null : e.WebErrorStatus);
        core.Navigate(page.AbsoluteUri);
        return await loaded.Task is { } error
            ? throw new DiagramRendererException(string.Format(CultureInfo.CurrentCulture, Strings.RendererUnavailable, error))
            : core;
    }

    public void Dispose()
    {
        _disposed = true;
        try
        {
            _controller?.Close();
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            // On exit the browser process may have ended first: there is nothing left to close.
        }

        _controller = null;
        _window?.Dispose();
        _window = null;
    }

    // The DevTools protocol stays enabled: renderer calls go through it. Nobody sees the page's windows.
    private static void Configure(CoreWebView2Settings settings)
    {
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsWebMessageEnabled = false;
    }
}
