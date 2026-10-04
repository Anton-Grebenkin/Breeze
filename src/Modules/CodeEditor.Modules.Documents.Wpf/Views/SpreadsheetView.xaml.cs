using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CodeEditor.Modules.Documents.ViewModels;

namespace CodeEditor.Modules.Documents.Wpf.Views;

/// <summary>
/// Excel and CSV view. The file is read when the tab is first shown; grid columns are built for the selected sheet: a
/// frozen row number plus column letters. Cells bind to the row indexer, so empty cells aren't stored. An unloaded view
/// unsubscribes from its ViewModel.
/// </summary>
public sealed partial class SpreadsheetView
{
    private const double RowNumberWidth = 48;
    private const double MaxColumnWidth = 480;

    private SpreadsheetViewerViewModel? _viewModel;

    public SpreadsheetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += OnVisibleChanged;
    }

    private void Attach()
    {
        var viewModel = IsLoaded ? DataContext as SpreadsheetViewerViewModel : null;
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
        BuildColumns(_viewModel.SelectedSheet);
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
        if (e.PropertyName == nameof(SpreadsheetViewerViewModel.SelectedSheet))
        {
            BuildColumns(_viewModel?.SelectedSheet);
        }
    }

    private void BuildColumns(SheetViewModel? sheet)
    {
        Grid.Columns.Clear();
        if (sheet is null)
        {
            return;
        }

        Grid.Columns.Add(new DataGridTextColumn
        {
            Header = string.Empty,
            Binding = new Binding(nameof(SheetRowViewModel.Number)) { Mode = BindingMode.OneTime },
            ElementStyle = (Style)Resources["Grid.RowNumber"],
            Width = RowNumberWidth,
        });
        for (var column = 1; column <= sheet.Columns.Count; column++)
        {
            Grid.Columns.Add(new DataGridTextColumn
            {
                Header = sheet.Columns[column - 1],
                Binding = new Binding("[" + column.ToString(CultureInfo.InvariantCulture) + "]") { Mode = BindingMode.OneTime },
                ElementStyle = (Style)Resources["Grid.Text"],
                MaxWidth = MaxColumnWidth,
            });
        }
    }
}
