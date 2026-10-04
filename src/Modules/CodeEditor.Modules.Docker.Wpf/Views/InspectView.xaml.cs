using System.ComponentModel;
using System.Windows;
using CodeEditor.Modules.Docker.ViewModels.Tabs;
using ICSharpCode.AvalonEdit.Search;

namespace CodeEditor.Modules.Docker.Wpf.Views;

/// <summary>
/// <c>docker inspect</c> output in a tab. Visual logic: the view model's text goes into a read-only editor (AvalonEdit
/// text is not a dependency property); search via the button and <c>Ctrl+F</c>.
/// </summary>
public sealed partial class InspectView
{
    private readonly SearchPanel _search;
    private InspectViewModel? _viewModel;

    public InspectView()
    {
        InitializeComponent();
        _search = ReadOnlyEditor.Configure(Editor, this);
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    private void Attach()
    {
        Detach();
        _viewModel = IsLoaded ? DataContext as InspectViewModel : null;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Editor.Document.Text = _viewModel.Text;
        }
    }

    private void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InspectViewModel.Text) && _viewModel is not null)
        {
            Editor.Document.Text = _viewModel.Text;
        }
    }

    private void OnFindClick(object sender, RoutedEventArgs e) => ReadOnlyEditor.OpenSearch(Editor, _search);
}
