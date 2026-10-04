using System.Windows;
using System.Windows.Automation;
using CodeEditor.Shell.Resources;
using CodeEditor.UI.Themes;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>Minimize, maximize/restore and close buttons for a custom-chrome window.</summary>
public sealed partial class CaptionButtons
{
    private Window? _window;

    public CaptionButtons()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window is not null)
        {
            _window.StateChanged += OnWindowStateChanged;
            UpdateMaximizeButton();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.StateChanged -= OnWindowStateChanged;
            _window = null;
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e) => UpdateMaximizeButton();

    private void UpdateMaximizeButton()
    {
        var maximized = _window?.WindowState == WindowState.Maximized;
        var label = maximized ? Strings.Restore : Strings.Maximize;

        MaximizeButton.Content = maximized ? IconGlyphs.ChromeRestore : IconGlyphs.ChromeMaximize;
        MaximizeButton.ToolTip = label;
        AutomationProperties.SetName(MaximizeButton, label);
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            SystemCommands.MinimizeWindow(_window);
        }
    }

    private void OnMaximizeOrRestore(object sender, RoutedEventArgs e)
    {
        if (_window is null)
        {
            return;
        }

        if (_window.WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(_window);
        }
        else
        {
            SystemCommands.MaximizeWindow(_window);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            SystemCommands.CloseWindow(_window);
        }
    }
}
