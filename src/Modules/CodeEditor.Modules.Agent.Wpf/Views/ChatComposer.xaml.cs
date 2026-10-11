using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CodeEditor.Modules.Agent.Services.Attachments;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.Modules.Agent.ViewModels.Models;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>
/// The chat input. Visual logic only: <c>Enter</c> sends, <c>Shift+Enter</c> adds a line, <c>Esc</c> stops; the
/// parameter menu and context details open above the box from a button or a palette command. Pasting a picture, copied
/// files or a long text attaches them instead of typing (drops are handled by <see cref="ChatView"/>).
/// </summary>
public sealed partial class ChatComposer
{
    private ChatViewModel? _viewModel;

    public ChatComposer()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // The text box cannot paste a picture or files, so its own Paste is disabled for them: enable and take it over.
        CommandManager.AddPreviewCanExecuteHandler(InputBox, OnPreviewCanExecute);
        CommandManager.AddPreviewExecutedHandler(InputBox, OnPreviewExecuted);
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

    private void OnPreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (e.Command == ApplicationCommands.Paste && AttachmentData.Clipboard() is { } data && AttachmentData.HasAttachable(data))
        {
            e.CanExecute = true;
            e.Handled = true;
        }
    }

    private void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste || _viewModel is null || AttachmentData.Clipboard() is not { } data)
        {
            return;
        }

        var attachments = _viewModel.Attachments;
        if (AttachmentData.Files(data) is { Count: > 0 } files)
        {
            e.Handled = true;
            _ = attachments.AddAsync(files);
        }
        else if (AttachmentData.HasImage(data) && AttachmentData.Png(data) is { } png)
        {
            e.Handled = true;
            _ = attachments.AddImageAsync(png);
        }
        else if (AttachmentData.Text(data) is { } text && PastedAttachments.IsLongText(text))
        {
            e.Handled = true;
            _ = attachments.AddTextAsync(text);
        }
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
