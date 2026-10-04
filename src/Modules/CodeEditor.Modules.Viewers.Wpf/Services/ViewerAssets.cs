using System.IO;
using CodeEditor.Modules.Viewers.Services;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Viewers.Wpf.Services;

/// <summary>
/// Viewer pages (ADR 0037): the <c>Viewers</c> folder next to the program at <see cref="ViewerAddresses.AssetsHost"/>,
/// and the open file's folder at its own address (<see cref="ViewerAddresses.HostFor"/>). The file folder allows only a
/// plain <c>&lt;img&gt;</c> or <c>&lt;video&gt;</c> load: cross-origin script reads (fetch) are denied.
/// </summary>
internal static class ViewerAssets
{
    public const string FolderName = "Viewers";

    public static string Folder { get; } = Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Exposes the page's own files and the shown file's folder.</summary>
    public static void Map(CoreWebView2 core, string fileFolder)
    {
        core.SetVirtualHostNameToFolderMapping(ViewerAddresses.AssetsHost, Folder, CoreWebView2HostResourceAccessKind.Deny);
        core.SetVirtualHostNameToFolderMapping(ViewerAddresses.HostFor(fileFolder), fileFolder, CoreWebView2HostResourceAccessKind.DenyCors);
    }
}
