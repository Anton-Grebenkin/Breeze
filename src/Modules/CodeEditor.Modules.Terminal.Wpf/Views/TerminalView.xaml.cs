using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.ViewModels;
using CodeEditor.Modules.Terminal.Wpf.Services;
using CodeEditor.Shell.Theming;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Terminal.Wpf.Views;

/// <summary>
/// The Terminal panel: terminal tabs and the xterm.js page (<see cref="TerminalPage"/>). Visual logic: each terminal
/// gets its kept output when the page is ready and new output as it arrives; keys, sizes, copy and paste come back;
/// keyboard focus in the page tells the ViewModel to send keys to the shell. The page lives as long as the panel.
/// </summary>
public sealed partial class TerminalView : UserControl
{
    private readonly TerminalWebEnvironment _environment;
    private readonly IThemeService _themes;
    private readonly ILogger _logger;
    private TerminalPanelViewModel? _viewModel;
    private TerminalPage? _page;

    // Focus requested before the page could take it: the first show creates the view and loads the page later.
    private bool _focusPending;

    public TerminalView(TerminalWebEnvironment environment, IThemeService themes, ILogger<TerminalView> logger)
    {
        _environment = environment;
        _themes = themes;
        _logger = logger;
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _themes.Changed += OnThemeChanged;
        _viewModel?.EnsureStarted();
        EnsurePage();
    }

    // Moving the panel unloads the view for a moment; the page and its terminals wait for it to return.
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _themes.Changed -= OnThemeChanged;
        Host.Content = null;
    }

    private void EnsurePage()
    {
        if (_page is not null)
        {
            Host.Content = _page.Control;
            return;
        }

        _page = new TerminalPage(_environment, _logger);
        _page.MessageReceived += OnPageMessage;
        _page.Control.GotFocus += (_, _) => _viewModel?.SetFocused(true);
        _page.Control.LostFocus += (_, _) => _viewModel?.SetFocused(false);
        Host.Content = _page.Control;
        _ = OpenPageAsync(_page);
    }

    private async Task OpenPageAsync(TerminalPage page)
    {
        if (!await page.OpenAsync(TerminalPalette.Background(this)))
        {
            Host.Content = new TextBlock
            {
                Text = Strings.TerminalUnavailable,
                Margin = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
            };
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is { } previous)
        {
            previous.Sessions.CollectionChanged -= OnSessionsChanged;
            previous.PropertyChanged -= OnPanelChanged;
            previous.FocusRequested -= OnFocusRequested;
            foreach (var session in previous.Sessions)
            {
                Detach(session);
            }
        }

        _viewModel = e.NewValue as TerminalPanelViewModel;
        if (_viewModel is { } next)
        {
            next.Sessions.CollectionChanged += OnSessionsChanged;
            next.PropertyChanged += OnPanelChanged;
            next.FocusRequested += OnFocusRequested;
            foreach (var session in next.Sessions)
            {
                Attach(session);
            }

            SendState();
            OnFocusRequested(this, EventArgs.Empty);
        }
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var session in e.OldItems?.OfType<TerminalSessionViewModel>() ?? [])
        {
            Detach(session);
            _page?.Post(TerminalPageMessages.Close(session.Id));
        }

        foreach (var session in e.NewItems?.OfType<TerminalSessionViewModel>() ?? [])
        {
            Attach(session);
            _page?.Post(TerminalPageMessages.Open(session.Id, session.Replay));
        }
    }

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalPanelViewModel.Active) && _viewModel?.Active is { } active)
        {
            _page?.Post(TerminalPageMessages.Show(active.Id));
        }
    }

    private void OnFocusRequested(object? sender, EventArgs e)
    {
        _focusPending = true;
        FocusPage();
    }

    private void FocusPage() => Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
    {
        if (_focusPending && _page is { IsReady: true } page)
        {
            _focusPending = false;
            page.Control.Focus();
            page.Post(TerminalPageMessages.Focus());
        }
    });

    private void Attach(TerminalSessionViewModel session)
    {
        session.Output += OnOutput;
        session.Cleared += OnCleared;
    }

    private void Detach(TerminalSessionViewModel session)
    {
        session.Output -= OnOutput;
        session.Cleared -= OnCleared;
    }

    private void OnOutput(object? sender, string text)
    {
        if (sender is TerminalSessionViewModel session)
        {
            _page?.Post(TerminalPageMessages.Output(session.Id, text));
        }
    }

    private void OnCleared(object? sender, EventArgs e)
    {
        if (sender is TerminalSessionViewModel session)
        {
            _page?.Post(TerminalPageMessages.Clear(session.Id));
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_page is { } page)
        {
            page.Control.DefaultBackgroundColor = TerminalPalette.Background(this);
            page.Post(TerminalPageMessages.ThemeChanged(TerminalPalette.Read(this, _themes.Current)));
        }
    }

    // The page loads and reloads empty: it gets every terminal with its kept output.
    private void SendState()
    {
        if (_page is not { } page || _viewModel is not { } panel)
        {
            return;
        }

        page.Post(TerminalPageMessages.Init(TerminalPalette.Read(this, _themes.Current), panel.FontSize));
        foreach (var session in panel.Sessions)
        {
            page.Post(TerminalPageMessages.Open(session.Id, session.Replay));
        }

        if (panel.Active is { } active)
        {
            page.Post(TerminalPageMessages.Show(active.Id));
        }
    }

    private void OnPageMessage(object? sender, TerminalPageMessage message)
    {
        var session = _viewModel?.Sessions.FirstOrDefault(candidate => candidate.Id == message.Id);
        switch (message.Type)
        {
            case TerminalPageMessages.Ready:
                SendState();
                FocusPage();
                break;
            case TerminalPageMessages.Input when message.Data is { } data:
                session?.Write(data);
                break;
            case TerminalPageMessages.Resize:
                session?.Resize(message.Columns, message.Rows);
                break;
            case TerminalPageMessages.Copy when message.Data is { } text:
                TryClipboard(() => Clipboard.SetText(text));
                break;
            case TerminalPageMessages.Paste when session is not null:
                TryClipboard(() => _page?.Post(TerminalPageMessages.PasteText(session.Id, Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty)));
                break;
            default:
                break;
        }
    }

    // Another program may hold the clipboard for a moment; the user simply copies or pastes again.
    private static void TryClipboard(Action action)
    {
        try
        {
            action();
        }
        catch (COMException)
        {
        }
    }
}
