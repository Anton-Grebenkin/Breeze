using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using CodeEditor.Shell.Zoom;
using CodeEditor.UI.Controls;

namespace CodeEditor.Shell.Wpf.Windowing;

/// <summary>
/// Applies the interface zoom (<see cref="WindowZoom"/>) to the main window: a layout scale on the content keeps text
/// and icons vector-sharp, popups (menus, tooltips) take the scale of the element they open from, and the caption
/// height follows the title bar. Ctrl + wheel zooms the interface, except over elements with their own wheel zoom
/// (<see cref="WheelZoom"/>).
/// </summary>
internal sealed class WindowZoomBinder : IDisposable
{
    private readonly Window _window;
    private readonly FrameworkElement _content;
    private readonly WindowZoom _zoom;
    private readonly WindowChrome? _chrome;
    private readonly double _captionHeight;

    public WindowZoomBinder(Window window, FrameworkElement content, WindowZoom zoom)
    {
        _window = window;
        _content = content;
        _zoom = zoom;
        _chrome = WindowChrome.GetWindowChrome(window);
        _captionHeight = _chrome?.CaptionHeight ?? 0;

        Apply();
        _zoom.PropertyChanged += OnZoomChanged;
        _window.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    public void Dispose()
    {
        _zoom.PropertyChanged -= OnZoomChanged;
        _window.PreviewMouseWheel -= OnPreviewMouseWheel;
    }

    private void OnZoomChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WindowZoom.Percent))
        {
            Apply();
        }
    }

    // At 100 % there is no transform at all: layout and text are exactly as without zoom.
    private void Apply()
    {
        var scale = _zoom.Scale;
        if (_zoom.Percent == ZoomLevels.Default)
        {
            _content.ClearValue(FrameworkElement.LayoutTransformProperty);
        }
        else
        {
            var transform = new ScaleTransform(scale, scale);
            transform.Freeze();
            _content.LayoutTransform = transform;
        }

        _chrome?.CaptionHeight = _captionHeight * scale;
    }

    // The window is the first stop of the tunneling event, so it decides before scroll viewers scroll.
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled
            || (Keyboard.Modifiers & ModifierKeys.Control) == 0
            || WheelZoom.IsWithinOwnZoom(e.OriginalSource as DependencyObject))
        {
            return;
        }

        e.Handled = true;
        _zoom.Wheel(e.Delta);
    }
}
