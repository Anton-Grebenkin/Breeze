using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Threading;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.UI.Controls;
using CodeEditor.UI.Markdown;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>
/// The agent chat. Visual logic only: input focus on <c>Ctrl+Alt+I</c>, the feed smoothly follows the incoming answer
/// (until the user scrolls up), a once-a-second tick for the running block's time, the "More actions" menu, and link
/// clicks forwarded to the view model.
/// </summary>
public sealed partial class ChatView
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly DispatcherTimer _ticker;
    private readonly ScrollFollower _follower;
    private ChatViewModel? _viewModel;

    /// <param name="colorizer">Code block highlighting; without the editor module code is shown in one color.</param>
    public ChatView(ICodeColorizer? colorizer)
    {
        InitializeComponent();
        MarkdownViewer.SetCodeColorizer(this, colorizer);
        AddHandler(MarkdownViewer.LinkClickedEvent, new EventHandler<MarkdownLinkClickedEventArgs>(OnLinkClicked));
        AddHandler(Hyperlink.ClickEvent, new RoutedEventHandler(OnStepLinkClicked));
        _ticker = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TickInterval };
        _ticker.Tick += (_, _) => _viewModel?.Feed.Tick();
        _follower = new ScrollFollower(MessagesScroll);
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => _ticker.Stop();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as ChatViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            OnFocusRequested(this, EventArgs.Empty);
        }
    }

    private void OnFocusRequested(object? sender, EventArgs e) => Composer.FocusInput();

    private void OnLinkClicked(object? sender, MarkdownLinkClickedEventArgs e) =>
        _viewModel?.OpenLinkCommand.Execute(e.Target);

    // A file link in a chain step carries its path in Tag; answer links are handled by MarkdownViewer itself.
    private void OnStepLinkClicked(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Hyperlink { Tag: string path })
        {
            e.Handled = true;
            _viewModel?.OpenLinkCommand.Execute(path);
        }
    }

    // Files and pictures from Windows, the explorer, an editor tab or a browser; plain text is left to the input box.
    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (_viewModel is null || !AttachmentData.HasAttachable(e.Data))
        {
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        DropHint.Visibility = Visibility.Visible;
    }

    // Leave also fires when the pointer moves between children: hide only when it is outside the chat.
    private void OnPreviewDragLeave(object sender, DragEventArgs e)
    {
        var point = e.GetPosition(this);
        if (point.X <= 0 || point.Y <= 0 || point.X >= ActualWidth || point.Y >= ActualHeight)
        {
            DropHint.Visibility = Visibility.Collapsed;
        }
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
        if (_viewModel is null || !AttachmentData.HasAttachable(e.Data))
        {
            return;
        }

        e.Handled = true;
        var attachments = _viewModel.Attachments;
        if (AttachmentData.Files(e.Data) is { Count: > 0 } files)
        {
            _ = attachments.AddAsync(files);
        }
        else if (AttachmentData.Png(e.Data) is { } png)
        {
            _ = attachments.AddImageAsync(png);
        }
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is not { } menu)
        {
            return;
        }

        menu.DataContext = DataContext;
        menu.PlacementTarget = MoreButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    // A new turn always scrolls to the bottom; the block clock ticks while the agent works.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChatViewModel.IsBusy) || _viewModel is null)
        {
            return;
        }

        if (_viewModel.IsBusy)
        {
            _follower.Follow();
            _ticker.Start();
        }
        else
        {
            _ticker.Stop();
        }
    }
}
