namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// The command-line switch for the browser process from the user's proxy setting (<c>browser.proxy</c>). Only an
/// address of the expected shape passes: a setting must not be able to add a switch of its own.
/// </summary>
public static class BrowserEngineArguments
{
    /// <returns>The switch, or an empty string for the system's proxy.</returns>
    public static string Build(BrowserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Proxy(options.Proxy) is { } proxy ? "--proxy-server=" + proxy : string.Empty;
    }

    /// <summary>The proxy as the browser takes it: <c>scheme://host:port</c>; <c>null</c> if the setting is empty or not an address.</summary>
    public static string? Proxy(string? setting) =>
        Uri.TryCreate(setting?.Trim(), UriKind.Absolute, out var proxy) && proxy.Scheme is "http" or "https" or "socks5" or "socks4" && proxy.Host.Length > 0
            ? $"{proxy.Scheme}://{proxy.IdnHost}:{proxy.Port}"
            : null;
}
