using System.Globalization;
using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Tabs;

namespace CodeEditor.Modules.Browser.Services.Agent;

/// <summary>
/// Approval cards of the <c>browser</c> tool: the site and what the agent wants to do there — open an address, run a
/// script, or act in a tab of the user's, where the card names the button or field as the page shows it.
/// </summary>
public sealed class BrowserPreviews(BrowserAgentTabs tabs)
{
    public async Task<IReadOnlyList<FileChangePreview>> CreateAsync(IDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var action = Text(arguments, "action");
        var tab = tabs.Peek(Text(arguments, "tab"), opens: action == BrowserAgentTools.Navigate);
        var site = tab?.Page.Url?.IdnHost ?? string.Empty;
        switch (action)
        {
            case BrowserAgentTools.Evaluate:
                return Card(Strings.EvaluateApprovalTitle, site, BrowserAgentTools.Required(Text(arguments, "script"), "script"));
            case BrowserAgentTools.Navigate or BrowserAgentTools.OpenTab:
                var url = BrowserAgentTools.Address(Text(arguments, "url"));
                return Card(tab is { IsAgent: false } && action == BrowserAgentTools.Navigate ? Strings.UserTabNavigateTitle : Strings.ApprovalTitle, url.IdnHost, "GET " + url.AbsoluteUri);
            case BrowserAgentTools.Upload:
                return Card(Strings.UploadApprovalTitle, site, BrowserAgentTools.Required(Text(arguments, "path"), "path") + " → " + await ElementAsync(tab, Text(arguments, "ref"), cancellationToken));
            default:
                return Card(tab is { IsAgent: false } ? Strings.UserTabApprovalTitle : Strings.SiteActionApprovalTitle, site, await DescribeAsync(action, arguments, tab, cancellationToken));
        }
    }

    private static async Task<string> DescribeAsync(string? action, IDictionary<string, object?> arguments, BrowserTab? tab, CancellationToken cancellationToken) => action switch
    {
        BrowserAgentTools.Click => Format(Strings.CardClick, await ElementAsync(tab, Text(arguments, "ref"), cancellationToken)),
        BrowserAgentTools.Type => Format(Strings.CardType, await ElementAsync(tab, Text(arguments, "ref"), cancellationToken), Text(arguments, "text") ?? string.Empty),
        BrowserAgentTools.Hover => Format(Strings.CardHover, await ElementAsync(tab, Text(arguments, "ref"), cancellationToken)),
        BrowserAgentTools.Drag => Format(Strings.CardDrag, await ElementAsync(tab, Text(arguments, "ref"), cancellationToken), await ElementAsync(tab, Text(arguments, "to"), cancellationToken)),
        BrowserAgentTools.Dialog => Format(Strings.CardDialog, ToolArguments.GetOptional<bool?>(arguments, "accept") == false ? Strings.DialogDismiss : Strings.DialogAccept),
        BrowserAgentTools.Press => Format(Strings.CardPress, Text(arguments, "key") ?? "?"),
        BrowserAgentTools.Back => Strings.Back,
        BrowserAgentTools.Forward => Strings.Forward,
        BrowserAgentTools.Reload => Strings.Reload,
        BrowserAgentTools.CloseTab => Format(Strings.CardCloseTab, tab?.Page.Url is { } url ? BrowserTabLines.Address(url) : tab?.Id ?? "?"),
        _ => action ?? "?",
    };

    // The element as the page names it: the user approves "Sync", not "e12". A page that does not answer leaves the ref.
    private static async Task<string> ElementAsync(BrowserTab? tab, string? reference, CancellationToken cancellationToken)
    {
        reference = BrowserAgentTools.Required(reference, "ref");
        if (tab is null)
        {
            return reference;
        }

        try
        {
            using var reply = JsonDocument.Parse(await tab.Page.EvaluateAsync(BrowserScripts.Describe(reference), cancellationToken));
            var root = reply.RootElement;
            return root.TryGetProperty("name", out var name) && root.TryGetProperty("role", out var role)
                ? Format(Strings.CardElement, name.GetString() ?? string.Empty, role.GetString() ?? string.Empty)
                : reference;
        }
        catch (Exception exception) when (exception is BrowserException or JsonException)
        {
            return reference;
        }
    }

    private static IReadOnlyList<FileChangePreview> Card(string title, string site, string text) =>
        [new FileChangePreview(ProposedChangeKind.Command, ".", string.Empty, text) { Title = title, Header = site }];

    private static string? Text(IDictionary<string, object?> arguments, string name) => ToolArguments.GetOptional<string>(arguments, name);

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
