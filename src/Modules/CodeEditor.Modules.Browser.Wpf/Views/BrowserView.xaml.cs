using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Modules.Browser.Wpf.Services;

namespace CodeEditor.Modules.Browser.Wpf.Views;

/// <summary>
/// The Browser panel hosts the browser control, which the engine owns for the app lifetime. When the panel leaves the
/// screen (another bottom panel tab) it releases the control, and the next panel takes it with the same page. The show
/// command puts the caret into the address bar.
/// </summary>
public sealed partial class BrowserView : System.Windows.Controls.UserControl
{
    private readonly WebViewBrowserEngine _engine;
    private BrowserViewModel? _viewModel;

    public BrowserView(WebViewBrowserEngine engine)
    {
        _engine = engine;
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Host.Content = _engine.Control;

    private void OnUnloaded(object sender, RoutedEventArgs e) => Host.Content = null;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
        }

        _viewModel = e.NewValue as BrowserViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;

            // The view is created on first panel show, after focus was already requested.
            OnFocusRequested(this, EventArgs.Empty);
        }
    }

    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            Keyboard.Focus(AddressBox);
            AddressBox.SelectAll();
        });
}
