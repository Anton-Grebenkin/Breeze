using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CodeEditor.Shell.Palette;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// Palette view. Visual logic only: focus, mapping keys to ViewModel commands, scrolling.
/// </summary>
public sealed partial class CommandPaletteView
{
    private CommandPaletteViewModel? _viewModel;
    private IInputElement? _focusBeforeOpen;
    private Window? _window;

    public CommandPaletteView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window is not null)
        {
            _window.Deactivated += OnWindowDeactivated;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Opened -= OnOpened;
        }

        _viewModel = e.NewValue as CommandPaletteViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Opened += OnOpened;

            // The view is created lazily while the palette is already open, so take focus right away.
            if (_viewModel.IsOpen)
            {
                OnOpened(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Focus is set at Input priority, after layout and render, because WPF won't focus an invisible element. The caret
    /// goes to the end so the user can type right after ">" or ":".
    /// </summary>
    private void OnOpened(object? sender, EventArgs e)
    {
        // Mode change while open: the binding has already updated the text, so move the caret before the next key.
        if (IsKeyboardFocusWithin)
        {
            QueryBox.CaretIndex = QueryBox.Text.Length;
            return;
        }

        _focusBeforeOpen = Keyboard.FocusedElement;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            Keyboard.Focus(QueryBox);
            QueryBox.CaretIndex = QueryBox.Text.Length;
        });
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommandPaletteViewModel.IsOpen) || _viewModel is null)
        {
            return;
        }

        if (!_viewModel.IsOpen && _focusBeforeOpen is not null)
        {
            Keyboard.Focus(_focusBeforeOpen);
            _focusBeforeOpen = null;
        }
    }

    private void OnQueryPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var command = e.Key switch
        {
            Key.Down => _viewModel?.MoveNextCommand,
            Key.Up => _viewModel?.MovePreviousCommand,
            Key.PageDown => _viewModel?.MovePageDownCommand,
            Key.PageUp => _viewModel?.MovePageUpCommand,
            Key.Enter => _viewModel?.AcceptCommand,
            Key.Escape => _viewModel?.CloseCommand,
            _ => null,
        };

        if (command is not null)
        {
            e.Handled = true;
            command.Execute(null);
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultList.SelectedItem is { } selected)
        {
            ResultList.ScrollIntoView(selected);
        }
    }

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: PaletteItem item })
        {
            e.Handled = true;
            _viewModel?.ExecuteItemCommand.Execute(item);
        }
    }

    private void OnBackdropMouseDown(object sender, MouseButtonEventArgs e) => _viewModel?.Close();

    private void OnWindowDeactivated(object? sender, EventArgs e) => _viewModel?.Close();
}
