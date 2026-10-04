using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodeEditor.UI.Controls;

/// <summary>
/// Keeps a feed pinned to the bottom of growing content, smoothly rather than jumping on each chunk. The catch-up is
/// exponential: each <see cref="TimeConstant"/> covers ~63% of the remaining distance, so frequent chunks blend into
/// steady motion. Scrolling up stops following; returning to the very bottom resumes it.
/// </summary>
public sealed class ScrollFollower : IDisposable
{
    private static readonly TimeSpan TimeConstant = TimeSpan.FromMilliseconds(70);

    /// <summary>Closer than this (px), jump to the end: fractions of a pixel aren't worth frames.</summary>
    private const double SnapDistance = 1;

    private readonly ScrollViewer _viewer;
    private long _lastFrame;
    private bool _animating;

    public ScrollFollower(ScrollViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        _viewer = viewer;
        _viewer.ScrollChanged += OnScrollChanged;
        _viewer.Unloaded += (_, _) => Stop();
    }

    /// <summary>The feed follows the end: new content scrolls into view.</summary>
    public bool IsFollowing { get; private set; } = true;

    /// <summary>Resumes following (e.g. on a new user message) and catches up with the end.</summary>
    public void Follow()
    {
        IsFollowing = true;
        Start();
    }

    public void Dispose()
    {
        _viewer.ScrollChanged -= OnScrollChanged;
        Stop();
    }

    // Content growth: catch up. Our own scrolling down isn't a user action; scrolling up is.
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0)
        {
            if (IsFollowing)
            {
                Start();
            }

            return;
        }

        if (e.VerticalChange < 0)
        {
            IsFollowing = false;
            Stop();
        }
        else if (_viewer.VerticalOffset >= _viewer.ScrollableHeight - SnapDistance)
        {
            IsFollowing = true;
        }
    }

    private void Start()
    {
        if (_animating)
        {
            return;
        }

        _animating = true;
        _lastFrame = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += OnFrame;
    }

    private void Stop()
    {
        if (_animating)
        {
            _animating = false;
            CompositionTarget.Rendering -= OnFrame;
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var elapsed = Stopwatch.GetElapsedTime(_lastFrame);
        _lastFrame = Stopwatch.GetTimestamp();
        var remaining = _viewer.ScrollableHeight - _viewer.VerticalOffset;
        if (!IsFollowing || remaining <= SnapDistance)
        {
            if (IsFollowing)
            {
                _viewer.ScrollToVerticalOffset(_viewer.ScrollableHeight);
            }

            Stop();
            return;
        }

        var share = 1 - Math.Exp(-elapsed.TotalMilliseconds / TimeConstant.TotalMilliseconds);
        _viewer.ScrollToVerticalOffset(_viewer.VerticalOffset + Math.Max(SnapDistance, remaining * share));
    }
}
