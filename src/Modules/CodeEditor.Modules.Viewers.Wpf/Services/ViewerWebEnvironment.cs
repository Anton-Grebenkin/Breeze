using CodeEditor.Core.Storage;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Viewers.Wpf.Services;

/// <summary>
/// One WebView2 environment for all SVG and media tabs: a shared browser process, created when such a tab is first
/// shown. It has its own data folder (<see cref="DataFolderName"/>), so its settings are independent of the editor's
/// browser and the PDF viewer. Audio and video never start without a user action: the page player does not ask for it,
/// and the environment's autoplay policy forbids it. A failed attempt (no WebView2 Runtime) is not cached. UI thread
/// only.
/// </summary>
public sealed class ViewerWebEnvironment(UserDataPaths paths)
{
    public const string DataFolderName = "WebView2.Viewers";

    private const string BrowserArguments = "--autoplay-policy=document-user-activation-required";

    private Task<CoreWebView2Environment>? _environment;

    /// <exception cref="WebView2RuntimeNotFoundException">WebView2 is not installed.</exception>
    public Task<CoreWebView2Environment> GetAsync()
    {
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
