namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// The editor's browser (ADR 0027, 0054): makes pages that share one profile — cookies, sign-ins and cache. Implemented
/// by the module's WebView2 view.
/// </summary>
public interface IBrowserEngine
{
    /// <summary>A new page; <paramref name="restored"/> is the address it opens when first shown or called.</summary>
    /// <param name="forAgent">
    /// The page is the agent's: a dialog it opens waits for the agent's answer instead of showing the browser's window.
    /// </param>
    IBrowserPage CreatePage(BrowserPageState? restored = null, bool forAgent = false);
}
