using CodeEditor.Modules.Agent.Contracts.Turn;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>Builds and test runs still going in the background, as the build module reports them.</summary>
internal sealed class FakeRunningJobs : IAgentRunningJobs
{
    public List<string> Items { get; } = [];

    public IReadOnlyList<string> Running() => [.. Items];
}
