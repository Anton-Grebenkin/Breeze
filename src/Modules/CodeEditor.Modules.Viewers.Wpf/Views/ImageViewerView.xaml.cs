using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CodeEditor.Modules.Viewers.ViewModels;
using CodeEditor.UI.Controls;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>
/// Image view. Visual logic: on-screen size from zoom and DPI (100 % is one image pixel per screen pixel), scrolling
/// after a zoom change keeps the point under the cursor or the window center in place (<see cref="ZoomAnchor"/>),
/// <c>Ctrl</c> wheel zooms, dragging is <see cref="ImagePanning"/>. Tab visibility goes to the view model: the first show
/// decodes the image, and a hidden tab releases a large image.
/// </summary>
public sealed partial class ImageViewerView
{
    private ImageViewerViewModel? _viewModel;

    // Wheel zoom anchor; without it, the window center.
    private Point? _anchor;

    public ImageViewerView()
    {
        InitializeComponent();
        _ = new ImagePanning(Scroller, Frame);
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += (_, _) => _viewModel?.SetShown(IsVisible);
        Scroller.SizeChanged += (_, _) => ReportViewport();
        // Ctrl + wheel zooms the picture here; elsewhere the window zooms the interface.
        WheelZoom.SetHasOwnZoom(Scroller, true);
        Scroller.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    // The window moved to a screen with another scale factor: 100 % must stay pixel for pixel.
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Rescale();
    }

    private void Attach()
    {
        var viewModel = IsLoaded ? DataContext as ImageViewerViewModel : null;
        if (ReferenceEquals(viewModel, _viewModel))
        {
            return;
        }

        Detach();
        _viewModel = viewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Zoom.PropertyChanged += OnZoomChanged;
        Rescale();
        _viewModel.SetShown(IsVisible);
    }

    private void Detach()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Zoom.PropertyChanged -= OnZoomChanged;
        _viewModel = null;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageViewerViewModel.Image))
        {
            UpdateFrame();
        }
    }

    private void OnZoomChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ZoomState.Scale))
        {
            UpdateFrame();
        }
    }

    private void Rescale()
    {
        ReportViewport();
        UpdateFrame();
    }

    // Zoom works in screen pixels, so the viewport is reported in them too.
    private void ReportViewport()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        _viewModel?.Zoom.SetViewport(Scroller.ActualWidth * dpi.DpiScaleX, Scroller.ActualHeight * dpi.DpiScaleY);
    }

    // On-screen size in WPF units: image pixels × scale / DPI.
    private void UpdateFrame()
    {
        if (_viewModel?.Image is not { } image)
        {
            Frame.Visibility = Visibility.Collapsed;
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var scale = _viewModel.Zoom.Scale;
        var before = new Size(Frame.ActualWidth, Frame.ActualHeight);
        var after = new Size(image.Size.Width * scale / dpi.DpiScaleX, image.Size.Height * scale / dpi.DpiScaleY);
        Frame.Width = after.Width;
        Frame.Height = after.Height;
        Frame.Visibility = Visibility.Visible;
        KeepAnchor(before, after);
    }

    private void KeepAnchor(Size before, Size after)
    {
        var pointer = _anchor ?? new Point(Scroller.ViewportWidth / 2, Scroller.ViewportHeight / 2);
        var horizontal = ZoomAnchor.Offset(Scroller.ViewportWidth, pointer.X, Scroller.HorizontalOffset, before.Width, after.Width);
        var vertical = ZoomAnchor.Offset(Scroller.ViewportHeight, pointer.Y, Scroller.VerticalOffset, before.Height, after.Height);
        Scroller.UpdateLayout();
        Scroller.ScrollToHorizontalOffset(horizontal);
        Scroller.ScrollToVerticalOffset(vertical);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel is null || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        e.Handled = true;
        _anchor = e.GetPosition(Scroller);
        _viewModel.Zoom.Wheel(e.Delta);
        _anchor = null;
    }
}
