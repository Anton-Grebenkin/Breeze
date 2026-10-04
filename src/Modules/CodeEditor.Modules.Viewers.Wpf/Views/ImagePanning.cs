using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>
/// Drags an image larger than the window with the left button; a hand cursor shows it can be moved. Clicking the image
/// focuses the scroller so arrows and PageUp/PageDown scroll it. Subscriptions are on elements of the same view and
/// live as long as it does.
/// </summary>
internal sealed class ImagePanning
{
    private readonly ScrollViewer _scroller;
    private readonly FrameworkElement _target;
    private Point _start;
    private double _left;
    private double _top;
    private bool _dragging;

    public ImagePanning(ScrollViewer scroller, FrameworkElement target)
    {
        _scroller = scroller;
        _target = target;
        target.MouseLeftButtonDown += OnDown;
        target.MouseMove += OnMove;
        target.MouseLeftButtonUp += OnUp;
        target.LostMouseCapture += (_, _) => _dragging = false;
        scroller.ScrollChanged += (_, _) => target.Cursor = CanPan ? Cursors.Hand : null;
    }

    private bool CanPan => _scroller.ScrollableWidth > 0 || _scroller.ScrollableHeight > 0;

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _scroller.Focus();
        if (!CanPan)
        {
            return;
        }

        _start = e.GetPosition(_scroller);
        _left = _scroller.HorizontalOffset;
        _top = _scroller.VerticalOffset;
        _dragging = _target.CaptureMouse();
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var position = e.GetPosition(_scroller);
        _scroller.ScrollToHorizontalOffset(_left - (position.X - _start.X));
        _scroller.ScrollToVerticalOffset(_top - (position.Y - _start.Y));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _target.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
}
