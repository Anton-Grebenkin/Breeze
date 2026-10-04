using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.Services;
using CodeEditor.Modules.Viewers.ViewModels;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>
/// The WebView2 page of an SVG or media tab. Created when the tab is first shown and released when the view is unloaded
/// (the tab closed or moved to another group). Page ready, a new file load number and a theme change all trigger a new
/// show: the page gets the file address and theme colors. Other page messages go to the view
/// (<see cref="MessageReceived"/>).
/// </summary>
internal sealed class WebViewerHost
{
    private readonly FrameworkElement _view;
    private readonly ContentControl _host;
    private readonly TextBlock _unavailable;
    private readonly WebViewerServices _services;
    private readonly WebPageKind _kind;
    private WebViewerViewModel? _viewModel;
    private ViewerPage? _page;

    public WebViewerHost(FrameworkElement view, ContentControl host, TextBlock unavailable, WebViewerServices services, WebPageKind kind)
    {
        _view = view;
        _host = host;
        _unavailable = unavailable;
        _services = services;
        _kind = kind;
        view.DataContextChanged += (_, _) => Attach();
        view.Loaded += (_, _) => Attach();
        view.Unloaded += (_, _) => Release();
        view.IsVisibleChanged += (_, _) => OnVisibleChanged();
    }

    /// <summary>A page message other than ready: drawing size, zoom, recording metadata, error.</summary>
    public event EventHandler<ViewerPageMessage>? MessageReceived;

    public WebViewerViewModel? ViewModel => _viewModel;

    public void Post(string message) => _page?.Post(message);

    private void Attach()
    {
        var viewModel = _view.IsLoaded ? _view.DataContext as WebViewerViewModel : null;
        if (ReferenceEquals(viewModel, _viewModel))
        {
            return;
        }

        Release();
        _viewModel = viewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelChanged;
        _services.Themes.Changed += OnThemeChanged;
        OnVisibleChanged();
    }

    // The view was unloaded or given another view model: the page is no longer needed.
    private void Release()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
            _services.Themes.Changed -= OnThemeChanged;
            _viewModel = null;
        }

        _host.Content = null;
        _page?.Dispose();
        _page = null;
    }

    private void OnVisibleChanged()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.SetShown(_view.IsVisible);
        if (_view.IsVisible)
        {
            EnsurePage();
        }
    }

    private void EnsurePage()
    {
        if (_page is not null || _viewModel is null)
        {
            return;
        }

        _unavailable.Visibility = Visibility.Collapsed;
        var page = _page = new ViewerPage(_services.Environment, _services.Logger, _kind.AutomationId, _kind.Name);
        page.MessageReceived += OnPageMessage;
        page.Unavailable += OnUnavailable;
        _host.Content = page.Control;
        _ = page.OpenAsync(_kind.Page, _viewModel.Folder, PagePalette.Background(_view));
    }

    private void OnPageMessage(object? sender, ViewerPageMessage message)
    {
        if (message.Type == ViewerPageMessages.Ready)
        {
            Show();
            return;
        }

        MessageReceived?.Invoke(this, message);
    }

    private void OnUnavailable(object? sender, string reason)
    {
        _host.Content = null;
        _unavailable.Text = string.Format(CultureInfo.CurrentCulture, Strings.WebViewUnavailable, reason);
        _unavailable.Visibility = Visibility.Visible;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WebViewerViewModel.Revision))
        {
            Show();
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Show();

    private void Show()
    {
        if (_page is { } page && _viewModel is { Source: { } source } viewModel)
        {
            page.Post(_kind.ShowMessage(viewModel, source, PagePalette.Read(_view, _services.Themes.Current)));
        }
    }
}
