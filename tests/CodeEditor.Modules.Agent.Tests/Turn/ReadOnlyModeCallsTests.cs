using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Web;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Approvals;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>
/// Ask and Plan refuse changes, not looking (ADR 0012): a tool that needs approval still runs there when the call
/// changes nothing — reading a web page, or a call its module names as looking (the browser reading a tab).
/// </summary>
public sealed class ReadOnlyModeCallsTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    private ChatViewModel Chat => _fixture.Chat;

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(AgentMode.Ask)]
    [InlineData(AgentMode.Plan)]
    public async Task CallsTheModuleNamesAsLooking_Run_TheRestAreRefused(AgentMode mode)
    {
        SetMode(mode);
        _fixture.Policies.Add(new PagesPolicy());
        _fixture.Conversation.Tools.Add(new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string action) => $"сделано: {action}", PagesPolicy.Tool)));
        _fixture.Client.CallTools(("c1", PagesPolicy.Tool, Action("look")), ("c2", PagesPolicy.Tool, Action("press"))).Reply("Ответ.");

        await Send("что на странице?");

        var results = ToolResults();
        Assert.Contains("сделано: look", results);
        Assert.DoesNotContain("сделано: press", results);
        Assert.Contains(results, result => result.Contains("изменения недоступны", StringComparison.Ordinal));
        Assert.DoesNotContain(Chat.Messages, message => message.Kind == ChatMessageKind.Approval);
    }

    // In a mode that changes code nothing is different: the module's usual approval decides.
    [Fact]
    public async Task AgentMode_RunsBothCalls()
    {
        _fixture.Policies.Add(new PagesPolicy());
        _fixture.Conversation.Tools.Add(new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string action) => $"сделано: {action}", PagesPolicy.Tool)));
        _fixture.Client.CallTools(("c1", PagesPolicy.Tool, Action("look")), ("c2", PagesPolicy.Tool, Action("press"))).Reply("Ответ.");

        await Send("нажми");

        Assert.Equal(["сделано: look", "сделано: press"], ToolResults().Order());
    }

    // An unfamiliar site still needs its card, and after it the page is read.
    [Fact]
    public async Task PageRead_InAskMode_GetsItsCard_AndRuns()
    {
        SetMode(AgentMode.Ask);
        _fixture.Conversation.Tools.Add(new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string url) => "страница", WebAgentTools.FetchName)));
        _fixture.Client.CallTool("c1", WebAgentTools.FetchName, new Dictionary<string, object?> { ["url"] = "https://example.org/" }).Reply("Ответ.");

        var sending = Send("что на странице?");
        (await WaitForCardAsync()).ApproveCommand.Execute(null);
        await sending;

        Assert.Equal(["страница"], ToolResults());
    }

    private void SetMode(AgentMode mode) =>
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, AutoMemory = false, Approvals = AgentApprovals.Edits, Mode = mode });

    private static Dictionary<string, object?> Action(string action) => new() { ["action"] = action };

    private List<string> ToolResults() =>
        [.. _fixture.Client.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Select(result => result.Result?.ToString() ?? string.Empty)];

    private Task Send(string text)
    {
        Chat.Input = text;
        return Chat.SendCommand.ExecuteAsync(null);
    }

    private async Task<ApprovalCardViewModel> WaitForCardAsync()
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (Chat.Messages.LastOrDefault(message => message.Approval is { IsPending: true })?.Approval is { } card)
            {
                return card;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Карточка подтверждения не появилась.");
    }

    /// <summary>A module whose tool looks or presses: looking needs no card and works in every mode.</summary>
    private sealed class PagesPolicy : IAgentApprovalPolicy
    {
        public const string Tool = "pages";

        public bool CanDecide(string toolName) => toolName == Tool;

        public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments) => true;

        public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) => false;

        public bool ReadsInEveryMode(string toolName, IDictionary<string, object?> arguments) => ToolArguments.Get<string>(arguments, "action") == "look";

        public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) => null;

        public void AllowAlways(string toolName, string rule)
        {
        }
    }
}
