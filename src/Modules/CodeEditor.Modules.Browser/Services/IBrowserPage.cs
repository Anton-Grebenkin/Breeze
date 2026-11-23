namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// A page of the editor's browser (ADR 0027, 0054): the content of one tab, seen by both the user and the agent.
/// Implemented by the module's WebView2 view; methods may be called from any thread, browser work runs on the UI
/// thread. Nothing loads until the page is first shown or called: a call shows the panel and waits until the browser
/// is ready. Disposing closes the page.
/// </summary>
public interface IBrowserPage : IDisposable
{
    /// <summary>Address of the open page; <c>null</c> when nothing is open.</summary>
    Uri? Url { get; }

    string Title { get; }

    bool CanGoBack { get; }

    bool CanGoForward { get; }

    bool IsLoading { get; }

    /// <summary>
    /// HTTP status of the open page's document; <c>null</c> before the first response. A 4xx or 5xx page still loads:
    /// the server's error page is what the user sees.
    /// </summary>
    int? StatusCode { get; }

    /// <summary>The site icon as PNG; empty until the page reports one.</summary>
    ReadOnlyMemory<byte> Favicon { get; }

    /// <summary>The page's requests, for the agent's <c>network</c> and <c>response</c> actions.</summary>
    BrowserNetworkLog Network { get; }

    /// <summary>The address, title, icon or loading state changed; raised on the UI thread.</summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// The page opens a window (a link "in a new window", a sign-in popup). A handler that sets
    /// <see cref="BrowserPopupEventArgs.Page"/> gets the window in that page, linked to this one as its opener;
    /// otherwise the address opens in this page.
    /// </summary>
    event EventHandler<BrowserPopupEventArgs>? PopupRequested;

    /// <summary>The page closed itself (<c>window.close()</c>, the end of a sign-in popup).</summary>
    event EventHandler? CloseRequested;

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

    /// <summary>Calls a DevTools protocol method on the page (key presses, scripts that await, response bodies).</summary>
    /// <param name="parameters">The method's parameters as a JSON object.</param>
    /// <returns>The result as JSON.</returns>
    /// <exception cref="BrowserException">The browser is unavailable or the call failed.</exception>
    Task<string> DevToolsAsync(string method, string parameters, CancellationToken cancellationToken);

    /// <summary>Console messages and page errors since the last call.</summary>
    IReadOnlyList<BrowserConsoleMessage> TakeConsole();
}
