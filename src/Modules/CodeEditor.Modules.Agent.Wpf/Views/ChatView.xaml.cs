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
