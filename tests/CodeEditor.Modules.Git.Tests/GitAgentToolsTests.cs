using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Git.Services.Agent;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// git tools over a fake process runner: git runs without a shell in the root, reads need no approval, changes go
/// through a card with commands, a branch without an upstream is pushed with one created, git errors reach the model.
/// </summary>
public sealed class GitAgentToolsTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\repo");
    private readonly FakeProcessRunner _runner = new();
    private readonly Workspace _workspace;
    private readonly GitAgentTools _tools;

    public GitAgentToolsTests()
    {
        _workspace = new Workspace(new FakeFileSystem().AddDirectory(Root).AddFile(Path.Combine(Root, "src", "A.cs")), new ContextKeyService(), NullLogger<Workspace>.Instance);
        _workspace.Open(Root);
        _tools = new GitAgentTools(new GitRunner(_runner, _workspace), _workspace, new PassThroughOutputStore());
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Read_RunsGitInTheRoot_WithoutShellPagerOrPrompts()
    {
        _runner.Returns(0, "## main...origin/main\n M src/A.cs\n");

        var result = await InvokeAsync(GitAgentTools.ReadName, new() { ["action"] = "status" });

        Assert.Equal("## main...origin/main\n M src/A.cs", result);
        var request = Assert.Single(_runner.Requests);
        Assert.Equal(("git", Root), (request.FileName, request.WorkingDirectory));
        Assert.Equal(["--no-pager", "-c", "core.quotepath=false", "-c", "color.ui=false", "status", "--short", "--branch"], request.Arguments);
        Assert.Equal("0", request.Environment["GIT_TERMINAL_PROMPT"]);
        Assert.True(request.HideSecretVariables);
    }

    [Fact]
    public async Task Read_PathIsRelativeToTheRoot_EmptyDiffSaysSo()
    {
        var result = await InvokeAsync(GitAgentTools.ReadName, new() { ["action"] = "diff", ["path"] = @"src\A.cs" });

        Assert.Equal("Изменений нет.", result);
        Assert.Equal(["--", "src/A.cs"], _runner.Requests[0].Arguments.TakeLast(2));
    }

    [Fact]
    public async Task Read_PathOutsideTheFolder_IsRefused() =>
        await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(GitAgentTools.ReadName, new() { ["action"] = "log", ["path"] = @"..\other" }));

    [Fact]
    public async Task GitError_GoesToTheModel_WithExitCode()
    {
        _runner.Returns(128, "fatal: not a git repository (or any of the parent directories): .git");

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(GitAgentTools.ReadName, new() { ["action"] = "status" }));

        Assert.Contains("128", error.Message, StringComparison.Ordinal);
        Assert.Contains("not a git repository", error.Message, StringComparison.Ordinal);
    }

    // The card shows exactly what git will do; changes go only through it.
    [Fact]
    public async Task Commit_PreviewShowsCommands_ThenAddAndCommitRun()
    {
        Dictionary<string, object?> arguments = new() { ["action"] = "commit", ["message"] = "Исправить расчёт", ["files"] = new[] { "src/A.cs" } };

        var preview = Assert.Single(await _tools.PreviewAsync(GitAgentTools.ChangeName, arguments, TestContext.Current.CancellationToken));
        _runner.Returns(0, string.Empty).Returns(0, "[main 1a2b3c4] Исправить расчёт\n 1 file changed");
        var result = await InvokeAsync(GitAgentTools.ChangeName, arguments);

        Assert.Equal((ProposedChangeKind.Command, "git add -- src/A.cs\ngit commit -m \"Исправить расчёт\""), (preview.Kind, preview.NewText));
        Assert.True(_tools.CreateTools().Single(tool => tool.Name == GitAgentTools.ChangeName).GetService<ApprovalRequiredAIFunction>() is not null);
        Assert.Equal(["commit", "-m", "Исправить расчёт"], _runner.Requests[^1].Arguments.TakeLast(3));
        Assert.Contains("1 file changed", result, StringComparison.Ordinal);
    }

    // No upstream: plain "git push" would fail, so push creates the branch in origin.
    [Fact]
    public async Task Push_WithoutUpstream_SetsIt()
    {
        _runner.Returns(128, "fatal: no upstream configured for branch 'feature'");

        var preview = Assert.Single(await _tools.PreviewAsync(GitAgentTools.ChangeName, new Dictionary<string, object?> { ["action"] = "push" }, TestContext.Current.CancellationToken));

        Assert.Equal("git push --set-upstream origin HEAD", preview.NewText);
    }

    [Fact]
    public void ReadTool_IsReadOnly_ChangeToolIsNot()
    {
        var tools = _tools.CreateTools().ToList();

        Assert.True(ReadOnlyAIFunction.IsReadOnly(tools.Single(tool => tool.Name == GitAgentTools.ReadName)));
        Assert.False(ReadOnlyAIFunction.IsReadOnly(tools.Single(tool => tool.Name == GitAgentTools.ChangeName)));
    }

    private async Task<string> InvokeAsync(string name, Dictionary<string, object?> arguments)
    {
        var tool = _tools.CreateTools().OfType<AIFunction>().Single(candidate => candidate.Name == name);
        return (await tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;
    }

    /// <summary>Returns long output as is; output files are tested in the agent module.</summary>
    private sealed class PassThroughOutputStore : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }
}
