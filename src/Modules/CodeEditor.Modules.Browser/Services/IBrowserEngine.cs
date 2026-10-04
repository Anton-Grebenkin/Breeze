namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// The editor's browser (ADR 0027): one page in the Browser panel, seen by both the user and the agent. Implemented by
/// the module's WebView2 view; methods may be called from any thread, browser work runs on the UI thread. Without a
/// panel, the first call shows it and waits until the browser is ready.
/// </summary>
public interface IBrowserEngine
{
    /// <summary>Address of the open page; <c>null</c> when nothing is open.</summary>
    Uri? Url { get; }

    string Title { get; }

    bool CanGoBack { get; }

    bool CanGoForward { get; }

    bool IsLoading { get; }

    /// <summary>The address, title or loading state changed; raised on the UI thread.</summary>
    event EventHandler? StateChanged;

    /// <summary>Opens the address and waits for the page to load.</summary>
    /// <exception cref="BrowserException">The browser is unavailable, or the page did not open or load in time.</exception>
    Task NavigateAsync(Uri url, CancellationToken cancellationToken);

    Task GoBackAsync(CancellationToken cancellationToken);

    Task GoForwardAsync(CancellationToken cancellationToken);

    Task ReloadAsync(CancellationToken cancellationToken);

    /// <summary>Waits until the page stops loading (after a click), at most <paramref name="timeout"/>.</summary>
    Task WaitForLoadAsync(TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Runs a script on the page.</summary>
    /// <returns>The script result as JSON.</returns>
    /// <exception cref="BrowserException">The browser is unavailable or no page is open.</exception>
    Task<string> EvaluateAsync(string script, CancellationToken cancellationToken);

    /// <summary>Screenshot of the open page (<see cref="BrowserScreenshot"/>); the panel is shown without focus.</summary>
    /// <exception cref="BrowserException">The browser is unavailable, no page is open or the capture failed.</exception>
    Task<byte[]> CaptureAsync(BrowserImageFormat format, CancellationToken cancellationToken);

    /// <summary>Console messages and page errors since the last call.</summary>
    IReadOnlyList<BrowserConsoleMessage> TakeConsole();
}
