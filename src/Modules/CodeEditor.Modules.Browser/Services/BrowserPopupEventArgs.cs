namespace CodeEditor.Modules.Browser.Services;

/// <summary>A page asks for a new window; the handler supplies the page that becomes it.</summary>
public sealed class BrowserPopupEventArgs(Uri? url) : EventArgs
{
    /// <summary>The address the window opens; <c>null</c> when the page fills it by script.</summary>
    public Uri? Url { get; } = url;

    /// <summary>A fresh page for the window; left <c>null</c>, the address opens in the asking page.</summary>
    public IBrowserPage? Page { get; set; }
}
