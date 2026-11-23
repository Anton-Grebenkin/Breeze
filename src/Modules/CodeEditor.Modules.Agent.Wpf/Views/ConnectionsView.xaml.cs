using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CodeEditor.Modules.Agent.ViewModels.Models;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>
/// The connections panel over the chat: takes focus on open, Esc returns to the chat; the key box, which cannot be
/// bound, hands its text to the form and is cleared with it; a choice button opens its menu under itself.
/// </summary>
public sealed partial class ConnectionsView
{
    private ConnectionsViewModel? _viewModel;

    public ConnectionsView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(() => BackButton.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel is not null)
        {
            _viewModel.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as ConnectionsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnKeyChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Key = KeyBox.Password;
        }
    }

    // The form was cleared after a connection was added.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionsViewModel.Key) && _viewModel is { Key.Length: 0 } && KeyBox.Password.Length > 0)
        {
            KeyBox.Clear();
        }
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
