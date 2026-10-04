using CodeEditor.Modules.Browser.Services;
using CodeEditor.Shell.ToolWindows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Browser.ViewModels;

/// <summary>
/// The Browser panel (ADR 0027): address bar, back, forward, reload. The view (WebView2) shows the page and the agent
/// drives the same view, so the user sees every action and can take over. An address without a scheme gets http for
/// <c>localhost:5000</c> and https otherwise. "Show: Browser" puts the caret into the address bar.
/// </summary>
public sealed partial class BrowserViewModel : ObservableObject, IFocusableContent, IDisposable
{
    private readonly IBrowserEngine _browser;

    public BrowserViewModel(IBrowserEngine browser)
    {
        _browser = browser;
        _browser.StateChanged += OnStateChanged;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoCommand))]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    public event EventHandler? FocusRequested;

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _browser.StateChanged -= OnStateChanged;

    /// <summary>Opens the address as if typed into the bar; load errors show on the panel.</summary>
    public Task OpenAsync(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        Address = url.AbsoluteUri;
        return GoAsync();
    }

    /// <summary>Address from the bar: https without a scheme, http for this machine.</summary>
    public static Uri? Normalize(string address)
    {
        var text = address.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            var local = text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || text.StartsWith("127.0.0.1", StringComparison.Ordinal);
            text = (local ? "http://" : "https://") + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" ? url : null;
    }

    [RelayCommand(CanExecute = nameof(CanGo))]
    private Task GoAsync() => RunAsync(token => Normalize(Address) is { } url ? _browser.NavigateAsync(url, token) : Task.CompletedTask);

    private bool CanGo() => Normalize(Address) is not null;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private Task BackAsync() => RunAsync(_browser.GoBackAsync);

    private bool CanGoBack() => _browser.CanGoBack;

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private Task ForwardAsync() => RunAsync(_browser.GoForwardAsync);

    private bool CanGoForward() => _browser.CanGoForward;

    [RelayCommand]
    private Task ReloadAsync() => RunAsync(_browser.ReloadAsync);

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        Error = null;
        try
        {
            await action(CancellationToken.None);
        }
        catch (BrowserException exception)
        {
            Error = exception.Message;
        }
    }

    // Address and title come from the browser: both the agent and link navigation change them.
    private void OnStateChanged(object? sender, EventArgs e)
    {
        Address = _browser.Url?.AbsoluteUri ?? Address;
        Title = _browser.Title;
        IsLoading = _browser.IsLoading;
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
    }
}
