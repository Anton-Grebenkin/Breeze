using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Command-line switches for the browser process from the user's settings: the proxy (<c>browser.proxy</c>) and the
/// servers that get the Windows account for Kerberos and NTLM sign-in (<c>browser.authServers</c>). Only values of the
/// expected shape pass: a setting must not be able to add a switch of its own.
/// </summary>
public static partial class BrowserEngineArguments
{
    /// <returns>The switches, or an empty string for the browser's defaults (the system proxy, sign-in in the intranet zone).</returns>
    public static string Build(BrowserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var switches = new List<string>(2);
        if (Proxy(options.Proxy) is { } proxy)
        {
            switches.Add("--proxy-server=" + proxy);
        }

        var servers = options.AuthServers.Select(static server => server.Trim()).Where(static server => HostPattern().IsMatch(server)).ToList();
        if (servers.Count > 0)
        {
            switches.Add("--auth-server-allowlist=" + string.Join(',', servers));
        }

        return string.Join(' ', switches);
    }

    /// <summary>The proxy as the browser takes it: <c>scheme://host:port</c>; <c>null</c> if the setting is empty or not an address.</summary>
    public static string? Proxy(string? setting) =>
        Uri.TryCreate(setting?.Trim(), UriKind.Absolute, out var proxy) && proxy.Scheme is "http" or "https" or "socks5" or "socks4" && proxy.Host.Length > 0
            ? $"{proxy.Scheme}://{proxy.IdnHost}:{proxy.Port}"
            : null;

    // "*.corp.example", "gitlab.corp.example", "10.0.0.*": host characters only.
    [GeneratedRegex(@"^[A-Za-z0-9*][A-Za-z0-9*.\-]{0,252}$")]
    private static partial Regex HostPattern();
}
