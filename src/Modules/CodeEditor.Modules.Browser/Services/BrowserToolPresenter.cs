using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Browser.Resources;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>Browser rows in the agent feed: "Browser: localhost:5000/login", "Browser: click e12".</summary>
public sealed class BrowserToolPresenter : IAgentToolPresenter
{
    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (call.Name != BrowserAgentTools.ToolName)
        {
            return null;
        }

        var action = call.Text("action");
        var detail = action switch
        {
            BrowserAgentTools.Navigate => Site(call.Text("url")),
            BrowserAgentTools.Click => Format(Strings.FeedClick, call.Text("ref") ?? "?"),
            BrowserAgentTools.Type => Format(Strings.FeedType, call.Text("ref") ?? "?"),
            BrowserAgentTools.Back => Strings.FeedBack,
            BrowserAgentTools.Reload => Strings.FeedReload,
            BrowserAgentTools.Console => Strings.FeedConsole,
            BrowserAgentTools.Screenshot => Strings.FeedScreenshot,
            _ => Strings.FeedSnapshot,
        };
        return new AgentToolView(AgentToolIcon.Web, Format(Strings.FeedTitle, detail))
        {
            IsExploration = action is BrowserAgentTools.Snapshot or BrowserAgentTools.Console or BrowserAgentTools.Screenshot,
        };
    }

    private static string Site(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed.Authority + parsed.AbsolutePath.TrimEnd('/') : url ?? "?";

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
