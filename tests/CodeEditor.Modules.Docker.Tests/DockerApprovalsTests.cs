using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Docker.Services;
using CodeEditor.Modules.Docker.Services.Agent;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Docker.Tests;

/// <summary>
/// Docker change approval: "Always allow" is per action, exec and rm always ask; feed rows for reads and changes.
/// </summary>
public sealed class DockerApprovalsTests
{
    private readonly TestOptionsMonitor<DockerOptions> _options = new(new DockerOptions { AlwaysAllow = ["compose_up"] });
    private readonly FakeSettingsService _settings = new();
    private readonly ActionApprovalPolicy _approvals;

    public DockerApprovalsTests() =>
        _approvals = new ActionApprovalPolicy(DockerApprovals.Rules, () => _options.CurrentValue.AlwaysAllow, _settings, NullLogger<ActionApprovalPolicy>.Instance);

    [Fact]
    public void AllowedAction_RunsWithoutCard_OthersAsk()
    {
        Assert.True(_approvals.IsPreapproved(DockerAgentTools.ChangeName, Action("compose_up")));
        Assert.False(_approvals.IsPreapproved(DockerAgentTools.ChangeName, Action("compose_down")));
        Assert.False(_approvals.CanDecide(DockerAgentTools.ReadName));
    }

    // A command in a container can do anything and removal cannot be undone: never always-allowed, even if set by hand.
    [Theory]
    [InlineData("exec")]
    [InlineData("rm")]
    public void ExecAndRemove_AlwaysAsk(string action)
    {
        _options.Set(new DockerOptions { AlwaysAllow = [action] });

        Assert.Null(_approvals.SuggestRule(DockerAgentTools.ChangeName, Action(action)));
        Assert.False(_approvals.IsPreapproved(DockerAgentTools.ChangeName, Action(action)));
        _approvals.AllowAlways(DockerAgentTools.ChangeName, action);
        Assert.Empty(_settings.Written);
    }

    [Fact]
    public void AllowAlways_AddsTheActionToSettings()
    {
        Assert.Equal("build", _approvals.SuggestRule(DockerAgentTools.ChangeName, Action("build")));

        _approvals.AllowAlways(DockerAgentTools.ChangeName, "build");

        Assert.Equal(new[] { "compose_up", "build" }, _settings.Written[DockerApprovals.AlwaysAllowKey]);
    }

    [Fact]
    public void Feed_ReadIsExploration_ExecShowsTheCommand()
    {
        var presenter = new DockerToolPresenter();

        var logs = presenter.Present(new AgentToolCall(DockerAgentTools.ReadName, new Dictionary<string, object?> { ["action"] = "logs", ["name"] = "api" }));
        var exec = presenter.Present(new AgentToolCall(DockerAgentTools.ChangeName,
            new Dictionary<string, object?> { ["action"] = "exec", ["name"] = "api", ["command"] = new[] { "dotnet", "ef", "database", "update" } }));

        Assert.Equal((AgentToolIcon.Container, "Docker: журнал api", true), (logs!.Icon, logs.Title, logs.IsExploration));
        Assert.Equal(("Docker: dotnet ef database update в api", false), (exec!.Title, exec.IsExploration));
        Assert.Null(presenter.Present(new AgentToolCall("git", new Dictionary<string, object?>())));
    }

    private static Dictionary<string, object?> Action(string action) => new() { ["action"] = action };
}
