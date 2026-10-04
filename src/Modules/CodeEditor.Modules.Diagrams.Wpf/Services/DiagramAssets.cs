using System.IO;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Diagrams.Wpf.Services;

/// <summary>
/// Diagram pages (ADR 0035): the <c>Diagrams</c> folder next to the app, opened in WebView2 via the virtual host
/// <c>https://diagrams.codeeditor.example/</c>, so the pages get a secure context and load scripts from the same origin.
/// Mermaid is the local <c>Diagrams/mermaid.min.js</c> copy next to the pages, so diagrams render offline.
/// </summary>
internal static class DiagramAssets
{
    public const string HostName = "diagrams.codeeditor.example";
    public const string FolderName = "Diagrams";

    public static string Folder { get; } = Path.Combine(AppContext.BaseDirectory, FolderName);

    public static Uri Renderer { get; } = new($"https://{HostName}/renderer.html");

    public static Uri Preview { get; } = new($"https://{HostName}/preview.html");

    /// <summary>Exposes the folder to the pages; other sites cannot access it.</summary>
    public static void Map(CoreWebView2 core) =>
        core.SetVirtualHostNameToFolderMapping(HostName, Folder, CoreWebView2HostResourceAccessKind.Deny);

    /// <summary>The page may navigate only to its own addresses, so links in a diagram lead nowhere.</summary>
    public static bool IsOwn(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && string.Equals(uri.Host, HostName, StringComparison.OrdinalIgnoreCase);
}
