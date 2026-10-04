using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Git.Services.Agent;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// git arguments for tool actions: model values never become git options, paths follow "--", commit is add + commit,
/// new files are committed only when named.
/// </summary>
public sealed class GitCommandsTests
{
    [Fact]
    public void Read_BuildsCompactCommands()
    {
        Assert.Equal(["status", "--short", "--branch"], GitCommands.Read(new GitRead(GitCommands.Status)));
        Assert.Equal(["diff", "--stat", "--patch", "--staged", "main", "--", "src/A.cs"], GitCommands.Read(new GitRead(GitCommands.Diff, "src/A.cs", "main", Staged: true)));
        Assert.Equal(["log", "-n", "100", "--date=short", "--format=%h %ad %an%d%n    %s", "--", "src"], GitCommands.Read(new GitRead(GitCommands.Log, "src", MaxCount: 500)));
        Assert.Equal(["show", "--stat", "--patch", "--date=short", "HEAD"], GitCommands.Read(new GitRead(GitCommands.Show)));
        Assert.Equal(["blame", "--date=short", "-L", "10,20", "--", "src/A.cs"], GitCommands.Read(new GitRead(GitCommands.Blame, "src/A.cs") { StartLine = 10, EndLine = 20 }));
    }

    [Fact]
    public void Commit_StagesNamedFiles_OrTrackedChanges_ThenCommits()
    {
        Assert.Equal(
            [["add", "--", "src/A.cs", "src/New.cs"], ["commit", "-m", "Исправить \"скидку\"\n\nПодробности"]],
            GitCommands.Change(new GitChange(GitCommands.Commit, "Исправить \"скидку\"\n\nПодробности", ["src/A.cs", "src/New.cs"])));
        Assert.Equal(["add", "--update"], GitCommands.Change(new GitChange(GitCommands.Commit, "Сообщение"))[0]);
    }

    // A model value must not become a git option ("--output=…"); a branch must not be empty or contain spaces.
    [Theory]
    [InlineData(GitCommands.Switch, "--orphan")]
    [InlineData(GitCommands.CreateBranch, "моя ветка")]
    public void BranchName_MustNotBeAnOption_OrHaveSpaces(string action, string branch) =>
        Assert.Throws<AgentToolException>(() => GitCommands.Change(new GitChange(action, Branch: branch)));

    [Fact]
    public void Revision_MustNotBeAnOption() =>
        Assert.Throws<AgentToolException>(() => GitCommands.Read(new GitRead(GitCommands.Diff, Revision: "--output=C:/x.txt")));

    [Fact]
    public void MissingArgument_AndUnknownAction_AreExplained()
    {
        Assert.Contains("message", Assert.Throws<AgentToolException>(() => GitCommands.Change(new GitChange(GitCommands.Commit))).Message, StringComparison.Ordinal);
        Assert.Contains("path", Assert.Throws<AgentToolException>(() => GitCommands.Read(new GitRead(GitCommands.Blame))).Message, StringComparison.Ordinal);
        Assert.Contains("status, diff", Assert.Throws<AgentToolException>(() => GitCommands.Read(new GitRead("checkout"))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Display_QuotesArgumentsWithSpaces() =>
        Assert.Equal("git commit -m \"Fix the \\\"total\\\"\"", GitCommands.Display(["commit", "-m", "Fix the \"total\""]));
}
