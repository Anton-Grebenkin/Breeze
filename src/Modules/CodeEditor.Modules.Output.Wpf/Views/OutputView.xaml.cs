using System.Windows;
using System.Windows.Threading;
using CodeEditor.Modules.Output.ViewModels;

namespace CodeEditor.Modules.Output.Wpf.Views;

/// <summary>
/// Output panel. While visible, polls the channel every <see cref="RefreshInterval"/> and scrolls to the end
/// if the user was at the bottom. A hidden panel does nothing.
/// </summary>
public sealed partial class OutputView
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Pixels: the user still counts as "at the bottom" with a fraction of a line left.</summary>
    private const double BottomTolerance = 2;

    private readonly DispatcherTimer _timer;

    public OutputView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer(RefreshInterval, DispatcherPriority.Background, OnTick, Dispatcher);
        _timer.Stop();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            RefreshAndFollow();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void OnTick(object? sender, EventArgs e) => RefreshAndFollow();

    private void RefreshAndFollow()
    {
        if (DataContext is not OutputViewModel viewModel)
        {
            return;
        }

        var wasAtBottom = OutputText.VerticalOffset + OutputText.ViewportHeight >= OutputText.ExtentHeight - BottomTolerance;
        if (viewModel.Refresh() && wasAtBottom)
        {
            OutputText.ScrollToEnd();
        }
    }
}
