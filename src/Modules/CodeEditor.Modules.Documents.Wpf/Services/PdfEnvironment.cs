using CodeEditor.Core.Storage;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Documents.Wpf.Services;

/// <summary>
/// One WebView2 environment for all PDF tabs: a shared browser process created when the first PDF is shown. Data lives
/// in its own folder, not the built-in browser's: their environment options may differ, and a shared folder requires
/// identical ones. A failed attempt (no WebView2 Runtime) is not cached, so the next tab tries again.
/// </summary>
public sealed class PdfEnvironment(UserDataPaths paths)
{
    public const string DataFolderName = "WebView2.Documents";

    private Task<CoreWebView2Environment>? _environment;

    /// <summary>Environment for a WebView2 control. UI thread only.</summary>
    public Task<CoreWebView2Environment> GetAsync()
    {
        if (_environment is { IsFaulted: true } or { IsCanceled: true })
        {
            _environment = null;
        }

        return _environment ??= CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: paths.File(DataFolderName));
    }
}
