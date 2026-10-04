using CodeEditor.Modules.Viewers.ViewModels;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>A WebView2 tab page: page file, element name for UI tests and screen readers, show message.</summary>
/// <param name="Page">A page from the <c>Viewers</c> folder.</param>
/// <param name="ShowMessage">Builds the show JSON for the page from the view model, file address and theme colors.</param>
internal sealed record WebPageKind(
    string Page,
    string AutomationId,
    string Name,
    Func<WebViewerViewModel, Uri, IReadOnlyDictionary<string, string>, string> ShowMessage);
