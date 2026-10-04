using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Git.Services.Agent;
using CodeEditor.Modules.Git.Services.Cli;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Real git in a temporary repository: a commit with Cyrillic, quotes and a newline reaches history as is, status and
/// log are readable, git errors reach the model. Skipped when git is not installed.
/// </summary>
public sealed class GitRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CodeEditor.GitTests", Guid.NewGuid().ToString("N"));
    private readonly ProcessRunner _runner = new();
    private readonly Workspace _workspace;
    private readonly GitAgentTools _tools;

    public GitRepositoryTests()
    {
        Directory.CreateDirectory(_root);
        _workspace = new Workspace(new PhysicalFileSystem(), new ContextKeyService(), NullLogger<Workspace>.Instance);
        _workspace.Open(_root);
        _tools = new GitAgentTools(new GitRunner(_runner, _workspace), _workspace, new PassThroughOutputStore());
    }

    public void Dispose()
    {
        _workspace.Dispose();
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task CommitWithQuotesAndNewLines_ReachesHistoryAsIs()
    {
        await InitAsync();
        await File.WriteAllTextAsync(Path.Combine(_root, "Заказ.cs"), "class Order { }\n", TestContext.Current.CancellationToken);

        await InvokeAsync(GitAgentTools.ChangeName, new() { ["action"] = "commit", ["message"] = "Добавить \"заказ\"\n\nВторая строка", ["files"] = new[] { "Заказ.cs" } });
        var log = await InvokeAsync(GitAgentTools.ReadName, new() { ["action"] = "log" });
        var status = await InvokeAsync(GitAgentTools.ReadName, new() { ["action"] = "status" });

        Assert.Contains("Добавить \"заказ\"", log, StringComparison.Ordinal);
        Assert.StartsWith("## ", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Заказ.cs", status, StringComparison.Ordinal);
        var body = await _runner.RunAsync(new ProcessRequest("git", ["log", "-1", "--format=%B"], _root), null, TestContext.Current.CancellationToken);
        Assert.Equal("Добавить \"заказ\"\n\nВторая строка", body.Output.Trim().ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task UnknownRevision_IsAGitErrorForTheModel()
    {
        await InitAsync();

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(GitAgentTools.ReadName, new() { ["action"] = "show", ["revision"] = "нет-такой-ветки" }));

        Assert.Contains("128", error.Message, StringComparison.Ordinal);
    }

    // A repository with a commit author; skips the test when git is missing.
    private async Task InitAsync()
    {
        string[][] setup = [["init", "--initial-branch=main"], ["config", "user.name", "Тест"], ["config", "user.email", "test@example.com"], ["config", "core.autocrlf", "false"]];
        foreach (var arguments in setup)
        {
            try
            {
                await _runner.RunAsync(new ProcessRequest("git", arguments, _root), null, TestContext.Current.CancellationToken);
            }
            catch (InvalidOperationException)
            {
                Assert.Skip("git is not installed.");
            }
        }
    }

    private async Task<string> InvokeAsync(string name, Dictionary<string, object?> arguments)
    {
        var tool = _tools.CreateTools().OfType<AIFunction>().Single(candidate => candidate.Name == name);
        return (await tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;
    }

    private sealed class PassThroughOutputStore : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }
}
