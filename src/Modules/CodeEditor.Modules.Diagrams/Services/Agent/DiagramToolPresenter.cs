using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services.Export;

namespace CodeEditor.Modules.Diagrams.Services.Agent;

/// <summary>
/// Diagram rows in the agent feed: "Check diagram docs/arch.mmd", "View diagram docs/README.md #2", "Diagram
/// docs/arch.mmd → PNG". Viewing is exploration, so the feed collapses consecutive rows. The feed marks diagram errors
/// itself: the tool reports them as a failure.
/// </summary>
public sealed class DiagramToolPresenter : IAgentToolPresenter
{
    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (call.Name != DiagramAgentTools.ToolName)
        {
            return null;
        }

        var action = call.Text("action");
        var subject = Subject(call);
        var title = action switch
        {
            DiagramAgentTools.View => Format(Strings.FeedView, subject),
            DiagramAgentTools.Render => Format(Strings.FeedRender, subject, FormatName(call.Text("format"))),
            _ => Format(Strings.FeedCheck, subject),
        };
        return new AgentToolView(AgentToolIcon.Image, title) { FilePath = call.Text("path"), IsExploration = action == DiagramAgentTools.View };
    }

    private static string Subject(AgentToolCall call)
    {
        var subject = call.Text("path") ?? Strings.FeedText;
        return call.Number("block") is { } block ? $"{subject} #{block.ToString(CultureInfo.CurrentCulture)}" : subject;
    }

    private static string FormatName(string? format) =>
        DiagramToolFormats.TryParse(format) == DiagramFormat.Png ? Strings.FormatPng : Strings.FormatSvg;

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
