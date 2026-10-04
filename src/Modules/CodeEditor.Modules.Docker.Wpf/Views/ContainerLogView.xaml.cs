using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CodeEditor.Modules.Docker.ViewModels.Tabs;
using ICSharpCode.AvalonEdit.Search;

namespace CodeEditor.Modules.Docker.Wpf.Views;

/// <summary>
/// Container logs in a tab. Visual logic: lines from the view model are appended, evicted ones removed from the start,
/// and auto-scroll keeps the end in view. Scrolling up (wheel, keys, scroll bar) turns auto-scroll off at once so new
/// lines do not jerk the text; reaching the end turns it back on, like a terminal. A toolbar button toggles it too.
/// </summary>
public sealed partial class ContainerLogView
{
    /// <summary>Tolerance in pixels: "at the end" even when a fraction of a line remains.</summary>
    private const double BottomTolerance = 4;

    private readonly SearchPanel _search;
    private ContainerLogViewModel? _viewModel;

    public ContainerLogView()
    {
        InitializeComponent();
        _search = ReadOnlyEditor.Configure(Editor, this);
        Editor.PreviewMouseWheel += (_, e) => UserScrolled(up: e.Delta > 0);
        Editor.PreviewKeyDown += OnEditorKeyDown;
        Editor.AddHandler(ScrollBar.ScrollEvent, new ScrollEventHandler(OnScrollBarScroll));
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    private bool IsAtEnd => Editor.VerticalOffset + Editor.ViewportHeight >= Editor.ExtentHeight - BottomTolerance;

    private void Attach()
    {
        Detach();
        _viewModel = IsLoaded ? DataContext as ContainerLogViewModel : null;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Changed += OnLinesChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Editor.Document.Text = string.Join('\n', _viewModel.Lines);
        FollowEnd();
    }

    private void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.Changed -= OnLinesChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private void OnLinesChanged(object? sender, LogLinesEventArgs e)
    {
        var document = Editor.Document;
        if (e.Reset)
        {
            document.Text = string.Empty;
            return;
        }

        document.BeginUpdate();
        try
        {
            var text = string.Join('\n', e.Added);
            document.Insert(document.TextLength, document.TextLength > 0 ? "\n" + text : text);
            if (e.Removed > 0)
            {
                var end = e.Removed < document.LineCount ? document.GetLineByNumber(e.Removed + 1).Offset : document.TextLength;
                document.Remove(0, end);
            }
        }
        finally
        {
            document.EndUpdate();
        }

        FollowEnd();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContainerLogViewModel.AutoScroll))
        {
            FollowEnd();
        }
    }

    private void FollowEnd()
    {
        if (_viewModel?.AutoScroll == true)
        {
            Editor.ScrollToEnd();
        }
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (e.Key is Key.Up or Key.PageUp || (e.Key == Key.Home && control))
        {
            UserScrolled(up: true);
        }
        else if (e.Key is Key.Down or Key.PageDown || (e.Key == Key.End && control))
        {
            UserScrolled(up: false);
        }
    }

    // Dragging the thumb means reading, so auto-scroll stops; releasing it at the end turns it back on.
    private void OnScrollBarScroll(object sender, ScrollEventArgs e) =>
        UserScrolled(up: e.ScrollEventType is ScrollEventType.SmallDecrement or ScrollEventType.LargeDecrement or ScrollEventType.First or ScrollEventType.ThumbTrack);

    private void UserScrolled(bool up)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (up)
        {
            _viewModel.AutoScroll = false;
            return;
        }

        // The position is known only after layout.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (_viewModel is { AutoScroll: false } viewModel && IsAtEnd)
            {
                viewModel.AutoScroll = true;
            }
        });
    }

    private void OnFindClick(object sender, RoutedEventArgs e) => ReadOnlyEditor.OpenSearch(Editor, _search);
}
