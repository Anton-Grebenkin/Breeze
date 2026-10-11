using System.Collections.Frozen;
using System.Globalization;
using CodeEditor.Modules.Browser.Resources;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Why a page did not open, for the model and the user. WebView2 reports a status name (<c>CannotConnect</c>,
/// <c>Unknown</c>); when no server answered, the likely cause is an app that isn't running, so the text says so. An
/// HTTP error page is not a failure: it loads, with its status (<see cref="IBrowserEngine.StatusCode"/>).
/// </summary>
public static class BrowserErrors
{
    private static readonly FrozenSet<string> NoAnswer = FrozenSet.ToFrozenSet(
        ["CannotConnect", "ConnectionAborted", "ConnectionReset", "Disconnected", "HostNameNotResolved", "ServerUnreachable", "Timeout", "Unknown"],
        StringComparer.Ordinal);

    public static string Describe(string status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return NoAnswer.Contains(status) ? string.Format(CultureInfo.CurrentCulture, Strings.ServerNotReachable, status) : status;
    }
}
