using CodeEditor.Modules.Agent.Contracts.Feed;

namespace CodeEditor.Modules.Agent.Services.History;

/// <summary>A tool row in the saved feed: icon and outcome, so a reopened chat looks the same.</summary>
public sealed record ChatTranscriptTool(AgentToolIcon Icon, string? Detail, string? FilePath, bool IsFailure, bool IsExploration)
{
    public AgentToolView ToView(string title) =>
        new(Icon, title) { Detail = Detail, FilePath = FilePath, IsFailure = IsFailure, IsExploration = IsExploration };
}
