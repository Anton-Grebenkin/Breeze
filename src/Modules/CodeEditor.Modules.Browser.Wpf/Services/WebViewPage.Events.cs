using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CodeEditor.Modules.Browser.Wpf.Services;

/// <summary>The page's WebView2 control: creation, browser events, windows it opens and recovery after a crash.</summary>
public sealed partial class WebViewPage
{
    private const string Blank = "about:blank";

    // A page that crashes again sooner than this keeps the browser's error page instead of reloading in a loop.
    private static readonly TimeSpan CrashLoopGap = TimeSpan.FromSeconds(30);

    // The address of a page that did not open: the tab keeps it, as a browser's address bar does.
    private string? _unreached;
    private long _lastCrash = long.MinValue;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = _host.Dispatcher.InvokeAsync(() =>
        {
            _suspend?.Stop();
            Fail(_ready, new BrowserException(Strings.TabClosed));
            _loading?.TrySetResult(Strings.TabClosed);
            CloseDialog();
            _control?.Dispose();
            _control = null;
        });
        _closed(this);
    }

    private WebView2 Create()
    {
        var control = new WebView2();
        AutomationProperties.SetAutomationId(control, "Browser.Page");
        _ = InitializeAsync(control, _ready);
        return control;
    }

    private async Task InitializeAsync(WebView2 control, TaskCompletionSource<CoreWebView2> ready)
    {
        try
        {
            await control.EnsureCoreWebView2Async(await _host.EnvironmentAsync());
            var core = control.CoreWebView2;
            core.Settings.IsPasswordAutosaveEnabled = core.Settings.IsGeneralAutofillEnabled = _host.Options.SavePasswords;
            Subscribe(core);
            if (_forAgent)
            {
                HoldDialogs(core);
            }

            foreach (var domain in (string[])["Runtime.enable", "Log.enable", "Network.enable"])
            {
                await core.CallDevToolsProtocolMethodAsync(domain, "{}");
            }

            if (_pending is { } restored)
            {
                _pending = null;
                var loading = _loading = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _restoring = loading.Task;
                core.Navigate(restored.AbsoluteUri);
            }

            ready.TrySetResult(core);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogUnavailable(_host.Logger, exception);
            Fail(ready, new BrowserException(Format(Strings.BrowserUnavailable, exception.Message), exception));
        }
    }

    private void Subscribe(CoreWebView2 core)
    {
        string? opening = null; // Source keeps the previous page's address when a navigation fails before any answer.
        core.NavigationStarting += (_, e) =>
        {
            (opening, _unreached) = (e.Uri, null);
            _loading ??= new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            IsLoading = true;
            Update(core);
        };

        // A server's error page (404, 500) loads like any page: the agent reads it, with its status.
        core.NavigationCompleted += (_, e) =>
        {
            StatusCode = e.HttpStatusCode > 0 ? e.HttpStatusCode : null;
            NeedsUser = false;
            var failed = !e.IsSuccess && e.HttpStatusCode <= 0;
            _unreached = failed && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled ? opening : null;
            Finish(core, failed ? BrowserErrors.Describe(e.WebErrorStatus.ToString(), opening ?? core.Source) : null);
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
        core.FaviconChanged += (_, _) => _ = UpdateFaviconAsync(core);
        core.NewWindowRequested += OnNewWindowRequested;
        core.WindowCloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        // The browser's own sign-in window: a call waiting for the load is told at once instead of timing out, and the
        // tab is shown to the user (BrowserAttention).
        core.BasicAuthenticationRequested += (_, _) =>
        {
            NeedsUser = true;
            EndLoading(Strings.SignInWindowShown);
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
        core.ProcessFailed += (_, e) => OnProcessFailed(core, e.ProcessFailedKind);
        foreach (var method in BrowserConsoleEvents.Methods)
        {
            core.GetDevToolsProtocolEventReceiver(method).DevToolsProtocolEventReceived += (_, e) => _console.Record(method, e.ParameterObjectAsJson);
        }

        foreach (var method in BrowserNetworkLog.Methods)
        {
            core.GetDevToolsProtocolEventReceiver(method).DevToolsProtocolEventReceived += (_, e) => Network.Record(method, e.ParameterObjectAsJson);
        }
    }

    // A window the page opens becomes a tab whose page is that window, so the two stay linked (window.opener, the end
    // of a sign-in). Without a taker the address opens here, as before tabs.
    private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var request = new BrowserPopupEventArgs(Uri.TryCreate(e.Uri, UriKind.Absolute, out var url) ? url : null);
        PopupRequested?.Invoke(this, request);
        e.Handled = true;
        if (request.Page is not WebViewPage target)
        {
            (sender as CoreWebView2)?.Navigate(e.Uri);
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            e.NewWindow = await target.ReadyAsync(show: false, CancellationToken.None);
        }
        catch (Exception exception) when (exception is BrowserException or COMException or InvalidOperationException)
        {
            LogPopupFailed(_host.Logger, exception);
        }
        finally
        {
            deferral.Complete();
        }
    }

    // A crashed page is reloaded once; if the whole browser process is gone, the control is made anew and the page
    // comes back at its address.
    private void OnProcessFailed(CoreWebView2 core, CoreWebView2ProcessFailedKind kind)
    {
        LogProcessFailed(_host.Logger, kind.ToString(), Url?.Host);
        var now = Environment.TickCount64;
        var again = _lastCrash != long.MinValue && now - _lastCrash < CrashLoopGap.TotalMilliseconds;
        _lastCrash = now;
        switch (kind)
        {
            case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                Recreate();
                break;
            case CoreWebView2ProcessFailedKind.RenderProcessExited or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive when !again:
                core.Reload();
                break;
        }
    }

    private void Recreate()
    {
        _host.ForgetEnvironment();
        var old = _control;
        (_control, _ready, IsWanted) = (null, NewReady(), true);
        _pending = Url is { Scheme: "http" or "https" } ? Url : null;
        (IsLoading, _restoring) = (false, null);
        EndLoading(Strings.BrowserRestarted);
        old?.Dispose();
        _host.NotifyPagesChanged();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task UpdateFaviconAsync(CoreWebView2 core)
    {
        try
        {
            using var stream = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes);
            Favicon = bytes.ToArray();
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or IOException or ArgumentException)
        {
            Favicon = default;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Finish(CoreWebView2 core, string? error)
    {
        IsLoading = false;
        _restoring = null;
        EndLoading(error);
        Update(core);
    }

    private void EndLoading(string? error)
    {
        var loading = _loading;
        _loading = null;
        loading?.TrySetResult(error);
    }

    private void Update(CoreWebView2 core)
    {
        if (_unreached is { } address && Uri.TryCreate(address, UriKind.Absolute, out var unreached))
        {
            (Title, Url) = (Url == unreached ? Title : string.Empty, unreached);
        }
        else if (core.Source != Blank || _restoring is null)
        {
            // A page being restored keeps its address and title until the load commits.
            Url = Uri.TryCreate(core.Source, UriKind.Absolute, out var url) ? url : null;
            Title = core.DocumentTitle ?? string.Empty;
        }

        CanGoBack = core.CanGoBack;
        CanGoForward = core.CanGoForward;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // The exception is read here so a call nobody awaited does not surface as an unobserved task error.
    private static void Fail(TaskCompletionSource<CoreWebView2> ready, BrowserException exception)
    {
        if (ready.TrySetException(exception))
        {
            _ = ready.Task.Exception;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The browser (WebView2) is not available")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A browser process failed ({Kind}) on {Site}")]
    private static partial void LogProcessFailed(ILogger logger, string kind, string? site);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A window opened by a page did not become a tab")]
    private static partial void LogPopupFailed(ILogger logger, Exception exception);
}
