using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Web;

/// <summary>Feed lines for web tools: "Page learn.microsoft.com", "Web search 'EF Core 10 bulk update'".</summary>
public sealed class WebToolPresenter : IAgentToolPresenter
{
    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Name switch
        {
            WebAgentTools.FetchName => new AgentToolView(AgentToolIcon.Web, Format(Strings.ToolWebFetch, Site(call.Text("url")))) { IsExploration = true },
            WebAgentTools.SearchName => new AgentToolView(AgentToolIcon.Web, Format(Strings.ToolWebSearch, call.Text("query") ?? "?")) { IsExploration = true },
            _ => null,
        };
    }

    private static string Site(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed.IdnHost + parsed.AbsolutePath.TrimEnd('/') : url ?? "?";

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
