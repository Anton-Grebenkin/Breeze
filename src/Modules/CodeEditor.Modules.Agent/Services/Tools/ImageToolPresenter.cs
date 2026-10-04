using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>Feed line for viewing an image ("Image docs/mockup.png"): an exploration step; a click opens the file.</summary>
public sealed class ImageToolPresenter : IAgentToolPresenter
{
    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Name == ImageAgentTools.ViewImageName
            ? new AgentToolView(AgentToolIcon.Image, string.Format(CultureInfo.CurrentCulture, Strings.ToolViewImage, call.Text("path") ?? "?"))
            {
                IsExploration = true,
                FilePath = call.Text("path"),
            }
            : null;
    }
}
