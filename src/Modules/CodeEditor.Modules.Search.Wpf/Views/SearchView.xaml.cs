using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CodeEditor.Modules.Search.ViewModels;

namespace CodeEditor.Modules.Search.Wpf.Views;

/// <summary>
/// Search panel. Visual logic only: focusing the box on <c>Ctrl+Shift+F</c>, arrow from the box into the results,
/// clicks and <c>Enter</c> on matches. Searching and opening live in <see cref="SearchViewModel"/>.
/// </summary>
public sealed partial class SearchView
{
    private SearchViewModel? _viewModel;

    public SearchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
        }

        _viewModel = e.NewValue as SearchViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;

            // The view is created on first show, after the focus request was raised.
            OnFocusRequested(this, EventArgs.Empty);
        }
    }

    /// <summary>As in VS Code: caret in the box with the text selected, ready for a new query.</summary>
    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            Keyboard.Focus(QueryBox);
            QueryBox.SelectAll();
        });

    private void OnFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        _viewModel?.SetFocused(IsKeyboardFocusWithin);

    private void OnQueryPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _viewModel?.SearchCommand.Execute(null);
        }
        else if (e.Key == Key.Down && Results.Items.Count > 0)
        {
            e.Handled = true;
            if (Results.ItemContainerGenerator.ContainerFromIndex(0) is TreeViewItem first)
            {
                first.IsSelected = true;
                first.Focus();
            }
        }
    }

    private void OnResultsPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Results.SelectedItem is SearchMatchViewModel match)
        {
            e.Handled = true;
            _viewModel?.OpenMatchCommand.Execute(match);
        }
    }

    private void OnMatchClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: SearchMatchViewModel match })
        {
            _viewModel?.PreviewMatchCommand.Execute(match);
        }
    }

    private void OnMatchDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: SearchMatchViewModel match })
        {
            e.Handled = true;
            _viewModel?.OpenMatchCommand.Execute(match);
        }
    }
}
