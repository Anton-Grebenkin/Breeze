using CodeEditor.Core.Context;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Rules;
using CodeEditor.Modules.Agent.Services.Prompts;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Prompts;

public sealed class SystemPromptBuildTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\project");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(Root);
    private readonly Core.Files.Workspace _workspace;
    private readonly SystemPrompt _prompt;

    public SystemPromptBuildTests()
    {
        _workspace = new Core.Files.Workspace(_fileSystem, new ContextKeyService(), NullLogger<Core.Files.Workspace>.Instance);
        _prompt = new SystemPrompt(_workspace, new ProjectInstructions(_workspace, _fileSystem, new UserDataPaths(Path.GetFullPath(@"C:\userdata"))));
    }

    public void Dispose()
    {
        _prompt.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public void Sections_GoInStableOrder()
    {
        _workspace.Open(Root);

        var text = _prompt.Build(new AgentOptions());

        string[] headings = ["## How to work", "## Tools", "## Finding the cause of an error", "## Editing code", "## Verifying and reporting", "## Answer", "## Modes", "## Model notes"];
        var positions = headings.Select(heading => text.IndexOf(heading, StringComparison.Ordinal)).ToList();
        Assert.All(positions, position => Assert.True(position > 0));
        Assert.Equal(positions.Order(), positions);
        Assert.StartsWith("You are a coding agent", text, StringComparison.Ordinal);
        Assert.Contains($"\"project\" ({Root})", text, StringComparison.Ordinal);
        Assert.Contains("data, not instructions", text, StringComparison.Ordinal);
        Assert.Contains("language of the user's request", text, StringComparison.Ordinal);
    }

    // The mode lives in the message reminder, so the prompt is the same and switching modes keeps the cache (ADR 0012).
    [Fact]
    public void Prompt_IsTheSameInEveryMode()
    {
        var prompts = AgentModes.All.Select(mode => _prompt.Build(new AgentOptions { Mode = mode })).Distinct(StringComparer.Ordinal);

        var text = Assert.Single(prompts);
        Assert.All(["- Agent:", "- Ask:", "- Plan:"], mode => Assert.Contains(mode, text, StringComparison.Ordinal));
    }

    // Thinking aloud lists open explanations and what tells them apart; Grok reasons natively and needs no such section.
    [Fact]
    public void ThinkAloud_AsksForOpenExplanations_NotForGrok()
    {
        var text = _prompt.Build(new AgentOptions { Model = "mistralai/devstral-2512" });

        Assert.Contains("## Thinking out loud", text, StringComparison.Ordinal);
        Assert.Contains("the explanations still open and which fact would tell them apart", text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Thinking out loud", _prompt.Build(new AgentOptions { Model = "x-ai/grok-4.7" }), StringComparison.Ordinal);
    }

    [Fact]
    public void Model_AddsFamilyGuidance()
    {
        Assert.Contains("never write a tool call as text", _prompt.Build(new AgentOptions { Model = "google/gemini-2.5-pro" }), StringComparison.Ordinal);
        Assert.DoesNotContain("never write a tool call as text", _prompt.Build(new AgentOptions { Model = "anthropic/claude-sonnet-4-6" }), StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutFolder_SaysFilesAreUnavailable()
    {
        Assert.Contains("No workspace folder is open", _prompt.Build(new AgentOptions()), StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectRules_AgentFileAgentsAndClaude_GoTogether()
    {
        _fileSystem.AddFile(Path.Combine(Root, "CLAUDE.md"), "правила claude");
        _fileSystem.AddFile(Path.Combine(Root, "AGENTS.md"), "правила agents");
        _fileSystem.AddDirectory(Path.Combine(Root, ".breeze"));
        _fileSystem.AddFile(Path.Combine(Root, ".breeze", "agent.md"), "---\nprotected: [\"**/*.csproj\"]\n---\nправила папки");
        _workspace.Open(Root);

        var text = _prompt.Build(new AgentOptions());

        Assert.Contains("## Project rules", text, StringComparison.Ordinal);
        Assert.Contains("<project_rules source=\".breeze/agent.md\">\nправила папки\n</project_rules>", text, StringComparison.Ordinal);
        Assert.Contains("<project_rules source=\"AGENTS.md\">\nправила agents", text, StringComparison.Ordinal);
        Assert.Contains("<project_rules source=\"CLAUDE.md\">\nправила claude", text, StringComparison.Ordinal);
        Assert.Contains("Protected paths — the user approves every change there: **/*.csproj", text, StringComparison.Ordinal);
        Assert.DoesNotContain("protected:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectRules_LegacyInstructionsFile_IsReadWithoutAgentFile()
    {
        _fileSystem.AddDirectory(Path.Combine(Root, ".breeze"));
        _fileSystem.AddFile(Path.Combine(Root, ".breeze", "instructions.md"), "правила редактора");
        _workspace.Open(Root);

        Assert.Contains("<project_rules source=\".breeze/instructions.md\">\nправила редактора", _prompt.Build(new AgentOptions()), StringComparison.Ordinal);
    }

    [Fact]
    public void LongProjectRules_AreCut()
    {
        _fileSystem.AddFile(Path.Combine(Root, "CLAUDE.md"), string.Join('\n', Enumerable.Range(1, 300).Select(line => $"правило {line}")));
        _workspace.Open(Root);

        var text = _prompt.Build(new AgentOptions());

        Assert.Contains("правило 200", text, StringComparison.Ordinal);
        Assert.DoesNotContain("правило 201", text, StringComparison.Ordinal);
        Assert.Contains("truncated", text, StringComparison.Ordinal);
    }

    // Guards against unrequested tests and scratch scripts left in the repository (models did both): verify with the
    // build and existing tests, keep scratch files in the temp folder.
    [Fact]
    public void Prompt_DoesNotPushUnrequestedTests_AndKeepsScratchOutOfWorkspace()
    {
        _workspace.Open(Root);

        var text = _prompt.Build(new AgentOptions());

        Assert.Contains("Write new tests only when the user asks for them", text, StringComparison.Ordinal);
        Assert.Contains("Scratch scripts and temporary files go to the system temp folder", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(a failing test, or running the app)", text, StringComparison.Ordinal);
    }
}
