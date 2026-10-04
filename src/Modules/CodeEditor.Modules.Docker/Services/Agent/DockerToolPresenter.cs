using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Docker.Resources;

namespace CodeEditor.Modules.Docker.Services.Agent;

/// <summary>
/// Docker rows in the agent feed ("Docker: logs of api", "Docker: dotnet ef database update in api", "Docker: compose
/// up"). Reads count as exploration, so the feed collapses consecutive ones; the feed itself marks failures.
/// </summary>
public sealed class DockerToolPresenter : IAgentToolPresenter
{
    private const string Unknown = "?";

    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Name switch
        {
            DockerAgentTools.ReadName => new AgentToolView(AgentToolIcon.Container, Format(Strings.FeedTitle, ReadDetail(call))) { IsExploration = true },
            DockerAgentTools.ChangeName => new AgentToolView(AgentToolIcon.Container, Format(Strings.FeedTitle, ChangeDetail(call))),
            _ => null,
        };
    }

    private static string ReadDetail(AgentToolCall call) => call.Text("action") switch
    {
        DockerCommands.Images => Strings.FeedImages,
        DockerCommands.Logs => Format(Strings.FeedLogs, call.Text("name") ?? Unknown),
        DockerCommands.Inspect => Format(Strings.FeedInspect, call.Text("name") ?? Unknown),
        DockerCommands.ComposePs => Strings.FeedComposeServices,
        DockerCommands.ComposeLogs => Strings.FeedComposeLogs,
        _ => Strings.FeedContainers,
    };

    private static string ChangeDetail(AgentToolCall call)
    {
        var name = call.Text("name") ?? Unknown;
        return call.Text("action") switch
        {
            DockerCommands.Build => Format(Strings.FeedBuild, call.Text("image") ?? call.Text("context") ?? "."),
            DockerCommands.Run => Format(Strings.FeedStart, call.Text("name") ?? call.Text("image") ?? Unknown),
            DockerCommands.Exec => Format(Strings.FeedExec, Command(call), name),
            DockerCommands.Start => Format(Strings.FeedStart, name),
            DockerCommands.Stop => Format(Strings.FeedStop, name),
            DockerCommands.Restart => Format(Strings.FeedRestart, name),
            DockerCommands.Remove => Format(Strings.FeedRemove, name),
            DockerCommands.ComposeUp => Strings.FeedComposeUp,
            DockerCommands.ComposeDown => Strings.FeedComposeDown,
            var action => action ?? Unknown,
        };
    }

    private static string Command(AgentToolCall call) =>
        call.Items("command") is { Count: > 0 } items ? string.Join(' ', items.Select(item => item.ToString())) : Unknown;

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
