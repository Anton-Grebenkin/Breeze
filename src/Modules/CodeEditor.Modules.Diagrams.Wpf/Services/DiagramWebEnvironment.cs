using CodeEditor.Core.Storage;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Diagrams.Wpf.Services;

/// <summary>
/// The WebView2 environment for diagrams, one per module: the hidden renderer and the preview tabs share the browser
/// process. Created on the first render. It has its own data folder (<see cref="DataFolderName"/>), so its settings do
/// not depend on the editor's browser. The hidden page is not throttled: background timers and rendering run at full
/// speed. UI thread only.
/// </summary>
public sealed class DiagramWebEnvironment(UserDataPaths paths)
{
    public const string DataFolderName = "Diagrams.WebView2";

    private const string BrowserArguments =
        "--disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows";

    private Task<CoreWebView2Environment>? _environment;

    /// <exception cref="WebView2RuntimeNotFoundException">WebView2 is not installed.</exception>
    public Task<CoreWebView2Environment> GetAsync()
    {
        // A failure (no WebView2) is not cached: once it is installed, the next diagram creates the environment.
        if (_environment is { IsFaulted: true } or { IsCanceled: true })
        {
            _environment = null;
        }

        return _environment ??= CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: paths.File(DataFolderName),
            options: new CoreWebView2EnvironmentOptions(BrowserArguments));
    }
}
