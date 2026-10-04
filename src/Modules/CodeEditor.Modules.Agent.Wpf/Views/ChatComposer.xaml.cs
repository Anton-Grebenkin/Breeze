using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.Modules.Agent.ViewModels.Models;
using CodeEditor.Shell.Wpf.Input;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>
/// The chat input. Visual logic only: <c>Enter</c> sends, <c>Shift+Enter</c> adds a line, <c>Esc</c> stops; the
/// parameter menu and context details open above the box from a button or a palette command; files dropped on the box
/// (from Windows, the explorer or an editor tab) are attached to the message.
/// </summary>
public sealed partial class ChatComposer
{
    private ChatViewModel? _viewModel;

    public ChatComposer()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public void FocusInput() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => Keyboard.Focus(InputBox));

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Model.PropertyChanged -= OnModelPropertyChanged;
        }

        _viewModel = e.NewValue as ChatViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Model.PropertyChanged += OnModelPropertyChanged;
        }
    }

    // Dropped files become attachments, folders are skipped; dragged text is left to the text box.
    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!FileDragData.HasPaths(e.Data))
        {
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is null || FileDragData.GetPaths(e.Data) is not { } files)
        {
            return;
        }

        e.Handled = true;
        _ = _viewModel.Attachments.AddAsync(files.Where(File.Exists));
    }

    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            _viewModel.SendCommand.Execute(null);
        }
        else if (e.Key == Key.Escape && _viewModel.IsBusy)
        {
            e.Handled = true;
            _viewModel.StopCommand.Execute(null);
        }
    }

    // Opened by the button or the palette command: placed above the button, focused for arrow keys.
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ModelSettingsViewModel.IsParametersOpen) || _viewModel is null)
        {
            return;
        }

        ParametersMenu.PlacementTarget = ParametersButton;
        ParametersMenu.Placement = PlacementMode.Top;
        ParametersMenu.IsOpen = _viewModel.Model.IsParametersOpen;
    }

    private void OnParametersMenuClosed(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Model.IsParametersOpen = false;
        }
    }

    private void OnContextPopupOpened(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => Keyboard.Focus(ContextNewChat));

    private void OnContextPopupKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        ContextPopup.IsOpen = false;
        FocusInput();
    }
}
