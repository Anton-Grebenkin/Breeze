using CodeEditor.Core.Storage;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Terminal.Wpf.Services;

/// <summary>
/// The WebView2 environment of the terminal page, created when the panel is first shown, with its own data folder.
/// A terminal in a hidden panel keeps receiving output, so background timers are not throttled. UI thread only.
/// </summary>
public sealed class TerminalWebEnvironment(UserDataPaths paths)
{
    public const string DataFolderName = "WebView2.Terminal";

    private const string BrowserArguments =
        "--disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows";

    private Task<CoreWebView2Environment>? _environment;

    /// <exception cref="WebView2RuntimeNotFoundException">WebView2 is not installed.</exception>
    public Task<CoreWebView2Environment> GetAsync()
    {
        // A failure is not cached: once WebView2 is installed, the panel opens without a restart.
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
