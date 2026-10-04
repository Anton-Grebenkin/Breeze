using System.ComponentModel;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.Services;
using CodeEditor.Modules.Viewers.ViewModels;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>
/// SVG drawing in a WebView2 page (<see cref="WebViewerHost"/>). Visual logic: zoom from buttons and keys is sent to
/// the page as a message; the page computes wheel and fit zoom itself and reports back, with no echo: the page never
/// receives its own zoom.
/// </summary>
public sealed partial class SvgViewerView
{
    private readonly WebViewerHost _page;
    private SvgViewerViewModel? _viewModel;

    // The zoom the page already shows, and whether a page reply is being handled: neither is sent back.
    private double? _pageScale;
    private bool _fromPage;

    public SvgViewerView(WebViewerServices services)
    {
        InitializeComponent();
        _page = new WebViewerHost(this, Host, Unavailable, services, new WebPageKind(ViewerAddresses.SvgPage, "SvgViewer.Page", Strings.Picture, ShowMessage));
        _page.MessageReceived += OnPageMessage;
        DataContextChanged += (_, _) => Track();
        Loaded += (_, _) => Track();
        Unloaded += (_, _) => Untrack();
    }

    private static string ShowMessage(WebViewerViewModel viewModel, Uri source, IReadOnlyDictionary<string, string> theme)
    {
        var zoom = ((SvgViewerViewModel)viewModel).Zoom;
        return ViewerPageMessages.ShowPicture(source, zoom.IsFit ? null : zoom.Scale, theme);
    }

    private void Track()
    {
        var viewModel = IsLoaded ? DataContext as SvgViewerViewModel : null;
        if (ReferenceEquals(viewModel, _viewModel))
        {
            return;
        }

        Untrack();
        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.Zoom.PropertyChanged += OnZoomChanged;
        }
    }

    private void Untrack()
    {
        if (_viewModel is not null)
        {
            _viewModel.Zoom.PropertyChanged -= OnZoomChanged;
            _viewModel = null;
        }

        _pageScale = null;
    }

    private void OnPageMessage(object? sender, ViewerPageMessage message)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (message.Type == ViewerPageMessages.Zoom && message.Value is { } value)
        {
            _pageScale = ZoomState.Clamp(value);
        }

        _fromPage = true;
        try
        {
            _viewModel.Receive(message);
        }
        finally
        {
            _fromPage = false;
        }
    }

    private void OnZoomChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_fromPage || _viewModel?.Zoom is not { } zoom)
        {
            return;
        }

        if (e.PropertyName == nameof(ZoomState.IsFit) && zoom.IsFit)
        {
            _page.Post(ViewerPageMessages.Fit);
        }
        else if (e.PropertyName == nameof(ZoomState.Scale) && !zoom.IsFit && zoom.Scale != _pageScale)
        {
            _pageScale = zoom.Scale;
            _page.Post(ViewerPageMessages.ZoomTo(zoom.Scale));
        }
    }
}
