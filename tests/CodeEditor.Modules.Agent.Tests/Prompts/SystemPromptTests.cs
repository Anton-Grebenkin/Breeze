using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Prompts;

public sealed class SystemPromptTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task OtherFolder_RebuildsInstructions()
    {
        var other = Path.GetFullPath(@"C:\other");
        _fixture.FileSystem.AddDirectory(other);
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.Client.Reply("один").Reply("два");

        await _fixture.SendAsync("первый");
        Assert.Contains(AgentFixture.Root, Instructions(), StringComparison.Ordinal);

        _fixture.Workspace.Open(other);
        await _fixture.SendAsync("второй");

        Assert.Contains(other, Instructions(), StringComparison.Ordinal);
        Assert.DoesNotContain(AgentFixture.Root + ")", Instructions(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("anthropic/claude-sonnet-5", "apply_edits", "exactly once", "apply_patch")]
    [InlineData("openai/gpt-6-luna", "apply_patch", "@@ <signature>", "apply_edits")]
    public async Task EditingRulesAndTool_FollowModelFamily(string model, string tool, string rule, string otherTool)
    {
        _fixture.Options.Set(new AgentOptions { Model = model });
        _fixture.ToolProviders.Add(new EditTools());
        _fixture.Client.Reply("ответ");

        await _fixture.SendAsync("вопрос");

        Assert.Contains(tool, Instructions(), StringComparison.Ordinal);
        Assert.Contains(rule, Instructions(), StringComparison.Ordinal);
        var tools = _fixture.Client.LastOptions!.Tools!.Select(candidate => candidate.Name).ToList();
        Assert.Contains(tool, tools);
        Assert.DoesNotContain(otherTool, tools);
    }

    private sealed class EditTools : Contracts.IAgentToolProvider
    {
        public IEnumerable<AITool> CreateTools() =>
        [
            AIFunctionFactory.Create(() => "ok", "apply_edits"),
            AIFunctionFactory.Create(() => "ok", "apply_patch"),
        ];
    }

    // The agent passes instructions either in ChatOptions or as a system message, so both are checked.
    private string Instructions() =>
        _fixture.Client.LastOptions?.Instructions
        ?? string.Join('\n', _fixture.Client.Requests[^1].Where(message => message.Role == ChatRole.System).Select(message => message.Text));
}
