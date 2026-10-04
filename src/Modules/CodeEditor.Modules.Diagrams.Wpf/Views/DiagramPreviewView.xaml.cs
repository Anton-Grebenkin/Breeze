using System.ComponentModel;
using System.Windows;
using CodeEditor.Modules.Diagrams.ViewModels;
using CodeEditor.Modules.Diagrams.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Diagrams.Wpf.Views;

/// <summary>
/// The diagram preview tab: a toolbar and a WebView2 page (<see cref="PreviewPage"/>). Visual logic: after each render
/// the page gets the whole state with theme colors, button zoom goes to it as a message, and wheel zoom and page keys
/// come back to the ViewModel (without echo: the page is never sent its own zoom). The page is created when the tab
/// first appears on screen, survives the view being moved and is released when the tab closes.
/// </summary>
public sealed partial class DiagramPreviewView : System.Windows.Controls.UserControl
{
    private readonly DiagramWebEnvironment _environment;
    private readonly ILogger _logger;
    private DiagramPreviewViewModel? _viewModel;
    private PreviewPage? _page;

    // The zoom the page already shows; it is not sent back.
    private double? _pageZoom;

    public DiagramPreviewView(DiagramWebEnvironment environment, ILogger<DiagramPreviewView> logger)
    {
        _environment = environment;
        _logger = logger;
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => EnsurePage();

    // The view left the screen: if the tab was closed the page is no longer needed; otherwise the view is being moved
    // and the page waits for it to return.
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Host.Content = null;
        if (_viewModel is null or { IsClosed: true })
        {
            ClosePage();
        }
    }

    private void EnsurePage()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_page is null)
        {
            _page = new PreviewPage(_environment, _logger);
            _page.MessageReceived += OnPageMessage;
            _ = _page.OpenAsync(PreviewPalette.Background(this));
        }

        Host.Content = _page.Control;
    }

    private void ClosePage()
    {
        Host.Content = null;
        if (_page is not null)
        {
            _page.MessageReceived -= OnPageMessage;
            _page.Dispose();
            _page = null;
        }
    }

    // The page belongs to its ViewModel: when the view gets another one (or the tab container is torn down), it closes.
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Rendered -= OnRendered;
            _viewModel.FitRequested -= OnFitRequested;
            _viewModel.PropertyChanged -= OnPropertyChanged;
            ClosePage();
        }

        _viewModel = e.NewValue as DiagramPreviewViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Rendered += OnRendered;
            _viewModel.FitRequested += OnFitRequested;
            _viewModel.PropertyChanged += OnPropertyChanged;
            if (IsLoaded)
            {
                EnsurePage();
            }
        }
    }

    private void OnRendered(object? sender, EventArgs e) => ShowState();

    private void OnFitRequested(object? sender, EventArgs e) => _page?.Post(DiagramPageMessages.Fit);

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiagramPreviewViewModel.Zoom) && _viewModel is { } view && view.Zoom != _pageZoom)
        {
            _pageZoom = view.Zoom;
            _page?.Post(DiagramPageMessages.Zoom(view.Zoom));
        }
    }

    private void OnPageMessage(object? sender, DiagramPageMessage message)
    {
        switch (message.Type)
        {
            case DiagramPageMessages.Ready:
                ShowState();
                break;
            case DiagramPageMessages.ZoomType when message.Zoom is { } zoom:
                _pageZoom = DiagramZoom.Clamp(zoom);
                _viewModel?.ReportZoom(zoom);
                break;
            case DiagramPageMessages.Key:
                _viewModel?.PressKey(message.Key);
                break;
            default:
                break;
        }
    }

    // The page takes the zoom from the state only on the first display; after that it arrives in zoom messages.
    private void ShowState()
    {
        if (_viewModel is { } view && _page is { } page)
        {
            _pageZoom = view.Zoom;
            page.Post(DiagramPageMessages.Show(view, PreviewPalette.Read(this)));
        }
    }
}
