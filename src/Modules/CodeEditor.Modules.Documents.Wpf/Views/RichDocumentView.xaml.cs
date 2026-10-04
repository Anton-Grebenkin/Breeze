using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using CodeEditor.Modules.Documents.ViewModels;
using CodeEditor.Modules.Documents.Wpf.Rendering;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Documents.Wpf.Views;

/// <summary>
/// Word and PowerPoint view. The document is read when the tab is first shown, formatted text is rebuilt from blocks on
/// every load (<see cref="FlowDocumentBuilder"/>), links open in the system browser. An unloaded view unsubscribes from
/// its ViewModel.
/// </summary>
public sealed partial class RichDocumentView
{
    private readonly ISystemShell _shell;
    private RichDocumentViewerViewModel? _viewModel;

    public RichDocumentView(ISystemShell shell)
    {
        _shell = shell;
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += OnVisibleChanged;
        Viewer.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnNavigate));
    }

    private void Attach()
    {
        var viewModel = IsLoaded ? DataContext as RichDocumentViewerViewModel : null;
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
        Show();
        OnVisibleChanged(this, default);
    }

    private void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel = null;
        }
    }

    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && _viewModel is not null)
        {
            _ = _viewModel.EnsureLoadedAsync();
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RichDocumentViewerViewModel.Document))
        {
            Show();
        }
    }

    private void Show() => Viewer.Document = _viewModel?.Document is { } document ? FlowDocumentBuilder.Build(document) : null;

    // Only web and mailto links: the OS must not launch anything else.
    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        if (e.Uri is { Scheme: "http" or "https" or "mailto" } uri)
        {
            _shell.OpenInBrowser(uri);
        }
    }
}
