using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Opens pages from other modules (a container port, a preview) in the built-in browser: the panel shows wherever it is
/// placed (an editor tab by default), the address goes into its bar and load errors show on the panel. Focus stays put:
/// after a click the user looks at the page, not the address bar.
/// </summary>
public sealed class BrowserPageOpener(BrowserViewModel browser, WorkbenchLayout layout) : IWebPageOpener
{
    public Task OpenAsync(Uri url)
    {
        layout.Show(BrowserModule.ToolWindowId, focus: false);
        return browser.OpenAsync(url);
    }
}
