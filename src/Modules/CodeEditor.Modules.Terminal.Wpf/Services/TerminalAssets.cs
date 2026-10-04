using System.IO;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Terminal.Wpf.Services;

/// <summary>
/// The terminal page: the <c>Terminal</c> folder next to the app, opened via the virtual host
/// <c>https://terminal.codeeditor.example/</c>, so the page loads xterm.js from the same origin and works offline.
/// </summary>
internal static class TerminalAssets
{
    public const string HostName = "terminal.codeeditor.example";
    public const string FolderName = "Terminal";

    public static string Folder { get; } = Path.Combine(AppContext.BaseDirectory, FolderName);

    public static Uri Page { get; } = new($"https://{HostName}/terminal.html");

    /// <summary>Exposes the folder to the page; other sites cannot access it.</summary>
    public static void Map(CoreWebView2 core) =>
        core.SetVirtualHostNameToFolderMapping(HostName, Folder, CoreWebView2HostResourceAccessKind.Deny);

    /// <summary>The page never leaves its own address: links in shell output open nothing.</summary>
    public static bool IsOwn(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && string.Equals(uri.Host, HostName, StringComparison.OrdinalIgnoreCase);
}
