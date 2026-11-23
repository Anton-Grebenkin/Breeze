namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// The editor's browser (ADR 0027, 0054): makes pages that share one profile — cookies, sign-ins and cache. Implemented
/// by the module's WebView2 view.
/// </summary>
public interface IBrowserEngine
{
    /// <summary>A new page; <paramref name="restored"/> is the address it opens when first shown or called.</summary>
    IBrowserPage CreatePage(BrowserPageState? restored = null);
}
