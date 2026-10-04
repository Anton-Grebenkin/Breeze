using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Services.Deep;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Deep;

/// <summary>
/// The explore subagent (ADR 0012): its own context on the helper model, read-only (only read-only commands), reports
/// to the main agent; its reads don't count for the agent; also available in Ask mode.
/// </summary>
public sealed class ExploreAgentTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private readonly List<string> _commands = [];

    public ExploreAgentTests()
    {
        _fixture.Conversation.Tools.Add(new ReadOnlyAIFunction(AIFunctionFactory.Create((string path) =>
        {
            var text = $"class {Path.GetFileNameWithoutExtension(path)} {{ }}";
            _fixture.FileState.RecordRead(Path.Combine(AgentFixture.Root, path), text, 1, 1);
            return "1\t" + text;
        }, "read_file")));
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create((string path) => "edited " + path, "edit"));
        _fixture.Conversation.Tools.Add(new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string command) =>
        {
            _commands.Add(command);
            return "ran " + command;
        }, "run_command")));
        _fixture.Policies.Add(new CommandPolicy());
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Explore_ResearchesInOwnContext_AndReturnsFindings()
    {
        _fixture.HelperClient
            .CallTools(("r1", "read_file", new Dictionary<string, object?> { ["path"] = "src/Pricing.cs" }), ("g1", "run_command", new Dictionary<string, object?> { ["command"] = "git log -n 3" }))
            .Reply("`src/Pricing.cs:1` — скидка считается в классе Pricing.");
        _fixture.Client
            .CallTool("e1", ExploreAgent.ToolName, new Dictionary<string, object?> { ["question"] = "где считается скидка?" })
            .Reply("Скидка — в Pricing.");

        await _fixture.SendAsync("как считается скидка?");

        var result = _fixture.Client.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single();
        Assert.Equal("`src/Pricing.cs:1` — скидка считается в классе Pricing.", result.Result?.ToString());
        var explorer = _fixture.HelperClient.Requests[0];
        Assert.Equal("где считается скидка?", explorer[0].Text);
        Assert.Equal(["read_file", "run_command"], _fixture.HelperClient.LastOptions!.Tools!.Select(tool => tool.Name).Order());
        Assert.Equal(ExploreAgent.Instructions, _fixture.HelperClient.LastOptions.Instructions);
        Assert.Equal(["git log -n 3"], _commands);
        // The explorer's reads don't count for the agent, which must read the file itself before editing.
        Assert.Contains("не прочитан", _fixture.FileState.CheckEditable(Path.Combine(AgentFixture.Root, "src/Pricing.cs"), "src/Pricing.cs", "class Pricing { }"), StringComparison.Ordinal);
    }

    // The explorer has no approval cards, so it never runs commands that change anything.
    [Fact]
    public async Task Explorer_RunsOnlyReadOnlyCommands()
    {
        _fixture.HelperClient
            .CallTool("c1", "run_command", new Dictionary<string, object?> { ["command"] = "Remove-Item src" })
            .Reply("Не смог удалить.");
        _fixture.Client
            .CallTool("e1", ExploreAgent.ToolName, new Dictionary<string, object?> { ["question"] = "что в src?" })
            .Reply("Готово.");

        await _fixture.SendAsync("исследуй");

        Assert.Empty(_commands);
        var refused = _fixture.HelperClient.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single();
        Assert.Contains("только команды чтения", refused.Result?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explore_IsAvailableInAskMode_AndShownAsExploration()
    {
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, Mode = AgentMode.Ask });
        _fixture.HelperClient.Reply("`README.md:1` — описание.");
        _fixture.Client.CallTool("e1", ExploreAgent.ToolName, new Dictionary<string, object?> { ["question"] = "о чём проект?" }).Reply("Ответ.");

        await _fixture.SendAsync("о чём проект?");

        var result = _fixture.Client.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single();
        Assert.Equal("`README.md:1` — описание.", result.Result?.ToString());
        var row = new WorkflowToolPresenter().Present(new AgentToolCall(ExploreAgent.ToolName, new Dictionary<string, object?> { ["question"] = "о чём проект?" }))!;
        Assert.Equal((AgentToolIcon.Explore, "Разведка: о чём проект?", true), (row.Icon, row.Title, row.IsExploration));
    }

    /// <summary>Test command policy: only git log is read-only.</summary>
    private sealed class CommandPolicy : IAgentApprovalPolicy
    {
        public bool CanDecide(string toolName) => toolName == "run_command";

        public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments) => IsReadOnly(toolName, arguments);

        public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) =>
            arguments.TryGetValue("command", out var command) && command?.ToString()?.StartsWith("git log", StringComparison.Ordinal) == true;

        public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) => null;

        public void AllowAlways(string toolName, string rule)
        {
        }
    }
}
