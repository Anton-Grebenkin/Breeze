using System.Globalization;
using System.Runtime.InteropServices;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Diagrams.Wpf.Services;

/// <summary>
/// WebView2 diagram renderer (ADR 0035): one hidden <c>renderer.html</c> page per module, created on the first render
/// and considered open once it has loaded Mermaid (a 3.6 MB local copy next to the app). Calls use the DevTools
/// <c>Runtime.evaluate</c> (<see cref="MermaidProtocol"/>), which awaits the page promise and returns the whole value.
/// WebView2 is used only on the UI thread, so calls are marshaled there and renders run one at a time. If the page hangs
/// (a diagram Mermaid never finishes) or crashes, it is closed and the next diagram opens a new one.
/// </summary>
public sealed partial class WebViewDiagramRenderer(IUiDispatcher dispatcher, DiagramWebEnvironment environment, ILogger<WebViewDiagramRenderer> logger)
    : IDiagramRenderer, IDisposable
{
    // Opening the page includes parsing Mermaid on a slow machine; a single large diagram takes seconds to render.
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private HiddenWebPage? _page;
    private Task<CoreWebView2>? _opening;

    public async Task<DiagramResult<string>> CheckAsync(string text, CancellationToken cancellationToken)
    {
        var source = MermaidSource.Prepare(text);
        return MermaidProtocol.ReadText(await CallAsync(MermaidProtocol.Check(source), cancellationToken), "type", source);
    }

    public async Task<DiagramResult<string>> RenderSvgAsync(string text, DiagramTheme theme, CancellationToken cancellationToken)
    {
        var source = MermaidSource.Prepare(text);
        return MermaidProtocol.ReadText(await CallAsync(MermaidProtocol.Svg(source, theme), cancellationToken), "svg", source);
    }

    public async Task<DiagramResult<byte[]>> RenderPngAsync(string text, DiagramTheme theme, DiagramPngSize size, CancellationToken cancellationToken)
    {
        var source = MermaidSource.Prepare(text);
        return MermaidProtocol.ReadPng(await CallAsync(MermaidProtocol.Png(source, theme, size), cancellationToken), source);
    }

    public void Dispose()
    {
        _page?.Dispose();
        _gate.Dispose();
    }

    private async Task<string> CallAsync(string parameters, CancellationToken cancellationToken)
    {
        // The call may come from a WebView2 page key handler (a theme switch shortcut while the preview has focus): the
        // browser process waits for that handler, so WebView2 is accessed only after it returns.
        await Task.Yield();
        await _gate.WaitAsync(cancellationToken);
        var call = RunExclusiveAsync(parameters);

        // Observe the error of a call nobody awaits anymore; otherwise it surfaces as unobserved.
        _ = call.ContinueWith(
            static finished => finished.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return await call.WaitAsync(cancellationToken);
    }

    // Cancellation only stops waiting: the page still runs the call, so the next one starts after it (or after the
    // timeout). Calls never interleave in the page, so one diagram's theme cannot leak into another.
    private async Task<string> RunExclusiveAsync(string parameters)
    {
        var limit = LoadTimeout;
        try
        {
            var core = await OpenAsync().WaitAsync(limit);
            limit = RenderTimeout;
            return await EvaluateAsync(core, parameters).WaitAsync(limit);
        }
        catch (Exception exception) when (exception is TimeoutException or COMException or InvalidOperationException or ArgumentException)
        {
            LogFailed(logger, exception);
            await dispatcher.InvokeAsync(Close);
            throw exception is TimeoutException
                ? new DiagramRendererException(Format(Strings.RendererTimedOut, (int)limit.TotalSeconds), exception)
                : new DiagramRendererException(Format(Strings.RendererScriptFailed, exception.Message), exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string> EvaluateAsync(CoreWebView2 core, string parameters)
    {
        Task<string>? pending = null;
        await dispatcher.InvokeAsync(() => pending = core.CallDevToolsProtocolMethodAsync(MermaidProtocol.Method, parameters));
        return await pending!;
    }

    // The page opens once; a failure is not cached, so the next diagram retries (e.g. once the network is back).
    private async Task<CoreWebView2> OpenAsync()
    {
        Task<CoreWebView2>? opening = null;
        await dispatcher.InvokeAsync(() =>
        {
            if (_opening is { IsFaulted: true } or { IsCanceled: true })
            {
                Close();
            }

            opening = _opening ??= OpenOnUiThreadAsync();
        });
        return await opening!;
    }

    /// <exception cref="DiagramRendererException">WebView2 is unavailable or Mermaid did not load.</exception>
    private async Task<CoreWebView2> OpenOnUiThreadAsync()
    {
        try
        {
            var page = _page = new HiddenWebPage();
            var core = await page.OpenAsync(await environment.GetAsync(), DiagramAssets.Renderer);
            MermaidProtocol.ReadReady(await core.CallDevToolsProtocolMethodAsync(MermaidProtocol.Method, MermaidProtocol.Ready));
            return core;
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or COMException or InvalidOperationException or ArgumentException)
        {
            LogUnavailable(logger, exception);
            throw new DiagramRendererException(Format(Strings.RendererUnavailable, exception.Message), exception);
        }
    }

    // UI thread only; the next call reopens the page.
    private void Close()
    {
        _page?.Dispose();
        _page = null;
        _opening = null;
    }

    private static string Format(string format, object argument) => string.Format(CultureInfo.CurrentCulture, format, argument);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The diagram renderer (WebView2) is not available")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The diagram renderer call failed; the page is closed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
