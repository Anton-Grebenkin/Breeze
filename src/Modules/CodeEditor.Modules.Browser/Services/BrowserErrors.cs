using System.Collections.Frozen;
using System.Globalization;
using CodeEditor.Modules.Browser.Resources;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Why a page did not open, for the model and the user. WebView2 reports a status name (<c>CannotConnect</c>,
/// <c>Unknown</c>). When no server answered on this machine, the likely cause is an app that isn't running, so the text
/// says so; for a site elsewhere that question misleads, and the text points at the address and the network. An HTTP
/// error page is not a failure: it loads, with its status (<see cref="IBrowserEngine.StatusCode"/>).
/// </summary>
public static class BrowserErrors
{
    private static readonly FrozenSet<string> NoAnswer = FrozenSet.ToFrozenSet(
        ["CannotConnect", "ConnectionAborted", "ConnectionReset", "Disconnected", "HostNameNotResolved", "ServerUnreachable", "Timeout", "Unknown"],
        StringComparer.Ordinal);

    /// <param name="url">The address that did not open; <c>null</c> if unknown, taken as this machine.</param>
    public static string Describe(string status, string? url = null)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (!NoAnswer.Contains(status))
        {
            return status;
        }

        return string.Format(CultureInfo.CurrentCulture, IsThisMachine(url) ? Strings.ServerNotReachable : Strings.SiteNotReachable, status);
    }

    private static bool IsThisMachine(string? url) =>
        !Uri.TryCreate(url, UriKind.Absolute, out var address)
        || address.IsLoopback
        || address.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
}
