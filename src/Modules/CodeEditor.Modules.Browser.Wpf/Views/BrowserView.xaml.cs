using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Modules.Browser.Wpf.Services;
using Microsoft.Web.WebView2.Wpf;

namespace CodeEditor.Modules.Browser.Wpf.Views;

/// <summary>
/// The Browser panel hosts the browser controls, which the pages own for the app lifetime: the control of every tab
/// that was shown or called sits here, and only the shown tab's one is visible, so background tabs keep working. When
/// the panel leaves the screen it releases the controls, and the next panel takes them with the same pages. The show
/// command puts the caret into the address bar; the history and "more" menus open under their buttons.
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
        IsKeyboardFocusWithinChanged += (_, _) => _viewModel?.SetFocused(IsKeyboardFocusWithin);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _engine.PagesChanged += OnPagesChanged;
        HostPages();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _engine.PagesChanged -= OnPagesChanged;
        for (var index = Pages.Children.Count - 1; index >= 0; index--)
        {
            Release(index);
        }

        _viewModel?.SetFocused(false);
    }

    private void OnPagesChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(HostPages);

    // A tab's page is hosted once it is shown or called, so tabs kept from the last run load only when opened. Hidden,
    // not collapsed: a background page keeps its size, which its layout and the agent's actions rely on. An empty tab
    // stays hidden too, so the panel shows its hint instead of the browser's white blank page.
    private void HostPages()
    {
        if (!IsLoaded)
        {
            return;
        }

        var pages = _engine.Pages;
        for (var index = Pages.Children.Count - 1; index >= 0; index--)
        {
            var control = ((Border)Pages.Children[index]).Child;
            if (!pages.Any(page => page.HasControl && ReferenceEquals(page.Control, control)))
            {
                Release(index);
            }
        }

        var shown = _viewModel?.ActiveTab?.Tab.Page;
        foreach (var page in pages)
        {
            var isShown = ReferenceEquals(page, shown);
            if (isShown || page.HasControl || page.IsWanted)
            {
                HostOf(page).Visibility = isShown && _viewModel?.IsPageShown == true ? Visibility.Visible : Visibility.Hidden;
                page.SetShown(isShown);
            }
        }
    }

    private Border HostOf(WebViewPage page)
    {
        var control = page.Control;
        foreach (Border host in Pages.Children)
        {
            if (ReferenceEquals(host.Child, control))
            {
                return host;
            }
        }

        control.GotFocus += OnPageFocusChanged;
        control.LostFocus += OnPageFocusChanged;
        var added = new Border { Child = control };
        Pages.Children.Add(added);
        return added;
    }

    private void Release(int index)
    {
        var host = (Border)Pages.Children[index];
        if (host.Child is WebView2 control)
        {
            control.GotFocus -= OnPageFocusChanged;
            control.LostFocus -= OnPageFocusChanged;
        }

        host.Child = null;
        Pages.Children.RemoveAt(index);
    }

    // The page is a native window: focus inside it is reported by the control, not by the panel.
    private void OnPageFocusChanged(object sender, RoutedEventArgs e) =>
        _viewModel?.SetFocused(IsKeyboardFocusWithin || (sender as UIElement)?.IsFocused == true);

    private void OnTabMouseDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BrowserTabViewModel tab)
        {
            return;
        }

        if (e.ChangedButton == MouseButton.Middle)
        {
            tab.CloseCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left)
        {
            tab.SelectCommand.Execute(null);
        }
    }

    // On the press, not the release: the list closes as soon as the address bar loses the caret.
    private void OnSuggestionMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is not null && (sender as FrameworkElement)?.DataContext is BrowserSuggestion suggestion)
        {
            _viewModel.Suggestions.Choose(suggestion);
            _viewModel.GoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnAddressLostFocus(object sender, KeyboardFocusChangedEventArgs e) => _viewModel?.Suggestions.DismissCommand.Execute(null);

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Tools.PropertyChanged -= OnToolsPropertyChanged;
            _viewModel.Tools.FindFocusRequested -= OnFindFocusRequested;
        }

        _viewModel = e.NewValue as BrowserViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Tools.PropertyChanged += OnToolsPropertyChanged;
            _viewModel.Tools.FindFocusRequested += OnFindFocusRequested;
            HostPages();

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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.PropertyName is nameof(BrowserViewModel.ActiveTab) or nameof(BrowserViewModel.IsPageShown))
        {
            HostPages();
        }
        else if (e.PropertyName == nameof(BrowserViewModel.IsHistoryOpen))
        {
            HistoryMenu.PlacementTarget = HistoryButton;
            HistoryMenu.Placement = PlacementMode.Bottom;
            HistoryMenu.IsOpen = _viewModel.IsHistoryOpen;
        }
    }

    private void OnFindFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            Keyboard.Focus(FindBox);
            FindBox.SelectAll();
        });

    private void OnToolsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BrowserPageToolsViewModel.IsMenuOpen) || _viewModel is null)
        {
            return;
        }

        ToolsMenu.PlacementTarget = MoreButton;
        ToolsMenu.Placement = PlacementMode.Bottom;
        ToolsMenu.IsOpen = _viewModel.Tools.IsMenuOpen;
    }

    private void OnToolsMenuClosed(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Tools.IsMenuOpen = false;
        }
    }

    private void OnHistoryMenuClosed(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.IsHistoryOpen = false;
        }
    }
}
