using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Browser.Wpf.Services;

/// <summary>
/// Background tabs are suspended: a page behind another tab that nobody — the user or the agent — used for a while
/// stops its scripts and lets the browser reclaim its memory. Showing or calling the page wakes it.
/// </summary>
public sealed partial class WebViewPage
{
    private static readonly TimeSpan SuspendAfter = TimeSpan.FromMinutes(5);

    private DispatcherTimer? _suspend;
    private bool _shown;
    private long _lastUse;

    /// <summary>The panel shows this page or puts it behind another tab. UI thread only.</summary>
    public void SetShown(bool shown)
    {
        if (_shown == shown)
        {
            return;
        }

        _shown = shown;
        _suspend?.Stop();
        if (!shown && !_disposed)
        {
            _suspend ??= new DispatcherTimer(SuspendAfter, DispatcherPriority.Background, OnSuspendDue, Dispatcher.CurrentDispatcher);
            _suspend.Start();
        }
    }

    // Every call goes through here on the UI thread: the page wakes and counts as used.
    private CoreWebView2 Awake(CoreWebView2 core)
    {
        _lastUse = Environment.TickCount64;
        if (core.IsSuspended)
        {
            core.Resume();
        }

        return core;
    }

    // The timer keeps running while the agent still calls the page; it stops once the page is suspended or shown.
    private async void OnSuspendDue(object? sender, EventArgs e)
    {
        if (_shown || _disposed || !_ready.Task.IsCompletedSuccessfully)
        {
            _suspend?.Stop();
            return;
        }

        if (IsLoading || Environment.TickCount64 - _lastUse < SuspendAfter.TotalMilliseconds)
        {
            return;
        }

        try
        {
            if (await _ready.Task.Result.TrySuspendAsync())
            {
                _suspend?.Stop();
            }
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            // A page that is still visible or already closed cannot be suspended; the next tick tries again.
        }
    }
}
