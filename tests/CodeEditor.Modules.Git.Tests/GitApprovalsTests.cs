using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Agent;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Repository change approval: "Always allow" is per action, push and restore always ask; feed rows for reads and changes.
/// </summary>
public sealed class GitApprovalsTests
{
    private readonly TestOptionsMonitor<GitOptions> _options = new(new GitOptions { AlwaysAllow = ["commit"] });
    private readonly FakeSettingsService _settings = new();
    private readonly ActionApprovalPolicy _approvals;

    public GitApprovalsTests() =>
        _approvals = new ActionApprovalPolicy(GitApprovals.Rules, () => _options.CurrentValue.AlwaysAllow, _settings, NullLogger<ActionApprovalPolicy>.Instance);

    [Fact]
    public void AllowedAction_RunsWithoutCard_OthersAsk()
    {
        Assert.True(_approvals.IsPreapproved(GitAgentTools.ChangeName, Action("commit")));
        Assert.False(_approvals.IsPreapproved(GitAgentTools.ChangeName, Action("switch")));
        Assert.False(_approvals.CanDecide(GitAgentTools.ReadName));
    }

    // Push leaves the machine and restore cannot be undone: "always allow" never applies, even if set by hand.
    [Theory]
    [InlineData("push")]
    [InlineData("restore")]
    public void PushAndRestore_AlwaysAsk(string action)
    {
        _options.Set(new GitOptions { AlwaysAllow = [action] });

        Assert.Null(_approvals.SuggestRule(GitAgentTools.ChangeName, Action(action)));
        Assert.False(_approvals.IsPreapproved(GitAgentTools.ChangeName, Action(action)));
        Assert.True(_approvals.AlwaysAsks(GitAgentTools.ChangeName, Action(action)));
        Assert.False(_approvals.AlwaysAsks(GitAgentTools.ChangeName, Action("commit")));
        _approvals.AllowAlways(GitAgentTools.ChangeName, action);
        Assert.Empty(_settings.Written);
    }

    [Fact]
    public void AllowAlways_AddsTheActionToSettings()
    {
        Assert.Equal("stash", _approvals.SuggestRule(GitAgentTools.ChangeName, Action("stash")));

        _approvals.AllowAlways(GitAgentTools.ChangeName, "stash");

        Assert.Equal(new[] { "commit", "stash" }, _settings.Written[GitApprovals.AlwaysAllowKey]);
    }

    [Fact]
    public void Feed_ReadIsExploration_ChangeNamesTheCommit()
    {
        var presenter = new GitToolPresenter();

        var diff = presenter.Present(new AgentToolCall(GitAgentTools.ReadName, new Dictionary<string, object?> { ["action"] = "diff", ["path"] = "src/A.cs" }));
        var commit = presenter.Present(new AgentToolCall(GitAgentTools.ChangeName, new Dictionary<string, object?> { ["action"] = "commit", ["message"] = "Исправить расчёт\n\nПодробности" }));

        Assert.Equal((AgentToolIcon.Git, "Изменения git в src/A.cs", true, "src/A.cs"), (diff!.Icon, diff.Title, diff.IsExploration, diff.FilePath));
        Assert.Equal(("Коммит: Исправить расчёт", false), (commit!.Title, commit.IsExploration));
        Assert.Null(presenter.Present(new AgentToolCall("read_file", new Dictionary<string, object?>())));
    }

    private static Dictionary<string, object?> Action(string action) => new() { ["action"] = action };
}
