using System.Security.Cryptography;
using System.Text;

namespace CodeEditor.Modules.Viewers.Services;

/// <summary>
/// WebView2 viewer page addresses (ADR 0037). The pages are the <c>Viewers</c> folder next to the program, served at
/// <c>https://viewers.codeeditor.example/</c>. A file is served from its folder's address
/// <c>https://file-&lt;folder hash&gt;.codeeditor.example/&lt;name&gt;?v=&lt;version&gt;</c>: the page sees only that file's
/// folder, different folders get different addresses, and the version (modification time and load number) keeps the
/// browser from showing a stale cached copy. The <c>.example</c> domain is reserved (RFC 2606) and never hits the network.
/// </summary>
public static class ViewerAddresses
{
    public const string Domain = "codeeditor.example";
    public const string AssetsHost = "viewers." + Domain;
    public const string SvgPage = "svg.html";
    public const string MediaPage = "media.html";

    private const string FileHostPrefix = "file-";

    // 8 bytes of SHA-256, 16 hex digits: a collision between two folders is practically impossible.
    private const int HashLength = 8;

    public static Uri Page(string name) => new($"https://{AssetsHost}/{name}");

    /// <summary>The host name for a folder: one folder, one address.</summary>
    public static string HostFor(string folder)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)).ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized)).AsSpan(0, HashLength);
        return $"{FileHostPrefix}{Convert.ToHexStringLower(hash)}.{Domain}";
    }

    /// <summary>The file address for the page; the host is the file's folder (<see cref="HostFor"/>).</summary>
    public static Uri FileAddress(string filePath, string version)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? throw new ArgumentException("The path has no folder.", nameof(filePath));
        return new Uri($"https://{HostFor(folder)}/{Uri.EscapeDataString(Path.GetFileName(filePath))}?v={Uri.EscapeDataString(version)}");
    }

    /// <summary>A page may navigate only to viewer pages: links lead nowhere.</summary>
    public static bool IsPage(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && string.Equals(uri.Host, AssetsHost, StringComparison.OrdinalIgnoreCase);
}
