using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Prompts;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.Modules.Agent.ViewModels.Composer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>Agent loop: the mode's step limit, repeated calls, parallel calls, truncated answers.</summary>
public sealed class AgentLoopTests : IDisposable
{
    private static readonly TimeSpan PairTimeout = TimeSpan.FromSeconds(5);

    private readonly AgentFixture _fixture = new();
    private readonly TaskCompletionSource _aStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _bStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _probes;

    public AgentLoopTests()
    {
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create((string path) => $"ok {path} #{Interlocked.Increment(ref _probes)}", "probe"));
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create(WaitPairAsync, "wait_pair"));
    }

    private ChatViewModel Chat => _fixture.Chat;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task TextBeforeToolCalls_IsProgress_AnswerHoldsOnlyTheResult()
    {
        _fixture.Client
            .SayAndCallTool("Смотрю структуру.", "c1", "probe", Path(1))
            .SayAndCallTool("Читаю точку входа.", "c2", "probe", Path(2))
            .Reply("Проект — веб-сервис на модулях.");

        await _fixture.SendAsync("изучи проект");

        Assert.Equal(
            [
                (ChatMessageKind.User, "изучи проект"),
                (ChatMessageKind.Progress, "Смотрю структуру."),
                (ChatMessageKind.Tool, "probe(path: f1)"),
                (ChatMessageKind.Progress, "Читаю точку входа."),
                (ChatMessageKind.Tool, "probe(path: f2)"),
                (ChatMessageKind.Assistant, "Проект — веб-сервис на модулях."),
            ],
            Chat.Messages.Select(message => (message.Kind, message.Text)));
    }

    [Fact]
    public void EmptyTextBlocks_AreNotSentToTheModel()
    {
        var call = new FunctionCallContent("c1", "probe", new Dictionary<string, object?> { ["path"] = "a" });
        List<ChatMessage> history =
        [
            new(ChatRole.User, "вопрос"),
            new(ChatRole.Assistant, [new TextContent(string.Empty), call]),
            new(ChatRole.Assistant, [new TextContent(" ")]),
        ];

        var sent = EmptyContentFilterChatClient.Clean(history);

        Assert.Equal(2, sent.Count);
        Assert.Same(history[0], sent[0]);
        Assert.Equal([call], sent[1].Contents);
    }

    [Fact]
    public async Task RequestLimit_EndsWithSummary_AndOffersToContinue()
    {
        for (var i = 0; i < AgentModes.RequestLimit + 5; i++)
        {
            _fixture.Client.CallTool($"c{i}", "probe", Path(i));
        }

        await _fixture.SendAsync("исследуй всё");

        Assert.Equal(AgentModes.RequestLimit, _fixture.Client.Requests.Count);
        Assert.Equal(AgentModes.RequestLimit - 1, _probes);
        Assert.IsType<NoneChatToolMode>(_fixture.Client.LastOptions!.ToolMode);
        Assert.Equal(PromptSections.EditorNote(BudgetChatClient.WrapUpRequest), _fixture.Client.Requests[^1][^1].Text);
        Assert.Contains(Chat.Messages, message => message.Kind == ChatMessageKind.Assistant && message.Text == ScriptedChatClient.WrapUpAnswer);
        var notice = Chat.Messages[^1];
        Assert.Equal(ChatMessageKind.Notice, notice.Kind);
        Assert.Contains($"лимит в {AgentModes.RequestLimit} запросов", notice.Text, StringComparison.Ordinal);
        Assert.True(Chat.ContinueCommand.CanExecute(null));
    }

    [Fact]
    public async Task PlanMode_AnswerOffersExecution_WhichSwitchesToAgent()
    {
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, Mode = AgentMode.Plan });
        _fixture.Client.Reply("План: 1. поправить a.cs").Reply("Сделал.");
        await _fixture.SendAsync("спланируй");
        Assert.Equal(ChatMessageKind.Handoff, Chat.Messages[^1].Kind);
        Assert.Contains("Mode: Plan", Reminder(_fixture.Client.Requests[^1][^1]), StringComparison.Ordinal);
        var planOptions = _fixture.Client.LastOptions!;

        await Chat.ExecutePlanCommand.ExecuteAsync(null);

        Assert.Equal(AgentMode.Agent, _fixture.Options.CurrentValue.Mode);
        Assert.Equal(ChatViewModel.ExecutePlanText, _fixture.Client.Requests[^1][^1].Question());
        Assert.Contains("Mode: Agent", Reminder(_fixture.Client.Requests[^1][^1]), StringComparison.Ordinal);
        // Same request prefix, so the provider cache survives the switch from planning to execution (ADR 0012).
        Assert.Equal(planOptions.Instructions, _fixture.Client.LastOptions!.Instructions);
        Assert.Equal(planOptions.Tools!.Select(tool => tool.Name), _fixture.Client.LastOptions.Tools!.Select(tool => tool.Name));
        Assert.Equal("Сделал.", Chat.Messages[^1].Text);
    }

    private static string Reminder(ChatMessage message) => message.Contents.OfType<TextContent>().Last().Text;

    [Fact]
    public async Task RepeatedCall_IsMarked_ThenBlocked()
    {
        _fixture.Client
            .CallTool("c1", "probe", Path(1))
            .CallTool("c2", "probe", Path(1))
            .CallTool("c3", "probe", Path(1))
            .Reply("хватит");

        await _fixture.SendAsync("читай");

        var results = _fixture.Client.Requests[^1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Select(result => result.Result?.ToString()).ToList();
        Assert.Equal("ok f1 #1", results[0]);
        Assert.EndsWith(GuardedToolFunction.RepeatNote, results[1], StringComparison.Ordinal);
        Assert.Equal(GuardedToolFunction.BlockedResult, results[2]);
        Assert.Equal(2, _probes);
    }

    [Fact]
    public async Task SameCallAfterSuccessfulEdit_IsNotARepeat()
    {
        var budget = new TurnBudget();
        budget.Begin();
        using var writeLock = new SemaphoreSlim(1);
        var build = new GuardedToolFunction(AIFunctionFactory.Create(() => "собрано", "build"), budget, writeLock, NullLogger.Instance);
        var edit = new GuardedToolFunction(new ApprovalRequiredAIFunction(AIFunctionFactory.Create((bool fail) => fail ? "Ошибка: не найден" : "изменено", "edit")), budget, writeLock, NullLogger.Instance);
        var token = TestContext.Current.CancellationToken;

        await build.InvokeAsync(new AIFunctionArguments(), token);
        await edit.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["fail"] = false }), token);
        var afterEdit = await build.InvokeAsync(new AIFunctionArguments(), token);
        await edit.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["fail"] = true }), token);
        var afterFailedEdit = await build.InvokeAsync(new AIFunctionArguments(), token);

        Assert.Equal("собрано", afterEdit?.ToString());
        Assert.EndsWith(GuardedToolFunction.RepeatNote, afterFailedEdit?.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AgentMode.Agent, true, true)]
    [InlineData(AgentMode.Ask, true, true)]
    [InlineData(AgentMode.Ask, false, false)]
    public async Task LongExploration_AsksForInterimSummary_ExceptExplorer(AgentMode mode, bool reflects, bool expected)
    {
        var budget = new TurnBudget();
        budget.Begin(mode, reflectsOnExploration: reflects);
        using var writeLock = new SemaphoreSlim(1);
        var read = new GuardedToolFunction(new ReadOnlyAIFunction(AIFunctionFactory.Create((int n) => $"строка {n}", "read")), budget, writeLock, NullLogger.Instance);
        var token = TestContext.Current.CancellationToken;

        var results = new List<string?>();
        for (var n = 1; n <= TurnBudget.ReadsBeforeReflection; n++)
        {
            results.Add((await read.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["n"] = n }), token))?.ToString());
        }

        Assert.DoesNotContain(results.SkipLast(1), result => result!.Contains(GuardedToolFunction.ExplorationNote, StringComparison.Ordinal));
        Assert.Equal(expected, results[^1]!.EndsWith(GuardedToolFunction.ExplorationNote, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EditOrBuild_RestartsExplorationCount()
    {
        var budget = new TurnBudget();
        budget.Begin();
        using var writeLock = new SemaphoreSlim(1);
        var read = new GuardedToolFunction(new ReadOnlyAIFunction(AIFunctionFactory.Create((int n) => $"строка {n}", "read")), budget, writeLock, NullLogger.Instance);
        var build = new GuardedToolFunction(AIFunctionFactory.Create((int n) => "собрано", "build"), budget, writeLock, NullLogger.Instance);
        var token = TestContext.Current.CancellationToken;

        string? last = null;
        for (var n = 1; n <= TurnBudget.ReadsBeforeReflection; n++)
        {
            if (n == TurnBudget.ReadsBeforeReflection / 2)
            {
                await build.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["n"] = n }), token);
            }

            last = (await read.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["n"] = n }), token))?.ToString();
        }

        Assert.DoesNotContain(GuardedToolFunction.ExplorationNote, last, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndependentCalls_RunInParallel()
    {
        _fixture.Client
            .CallTools(("a", "wait_pair", new Dictionary<string, object?> { ["name"] = "a" }), ("b", "wait_pair", new Dictionary<string, object?> { ["name"] = "b" }))
            .Reply("оба прочитаны");

        await _fixture.SendAsync("прочитай два файла");

        var results = _fixture.Client.Requests[^1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Select(result => result.Result?.ToString());
        Assert.Equal(["встретились a", "встретились b"], results.Order());
    }

    [Fact]
    public async Task EndlessTruncation_EndsWithHint_NotSilently()
    {
        _fixture.Client.ReplyTruncated(string.Empty).ReplyTruncated(string.Empty).ReplyTruncated(string.Empty);

        await _fixture.SendAsync("вопрос");

        Assert.Equal(AgentTurn.MaxLengthContinuations + 1, _fixture.Client.Requests.Count);
        Assert.Equal((ChatMessageKind.Status, AgentTurn.LengthLimitNotice), (Chat.Messages[^1].Kind, Chat.Messages[^1].Text));
    }

    [Fact]
    public void CallWithoutArguments_IsSentWithEmptyObject()
    {
        var sent = EmptyContentFilterChatClient.Clean([new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c1", "manage_todo")])]);

        var call = Assert.IsType<FunctionCallContent>(Assert.Single(Assert.Single(sent).Contents));
        Assert.NotNull(call.Arguments);
        Assert.Empty(call.Arguments);
    }

    // Without a default answer limit ProxyAPI reserves the model's maximum on the balance and may reply 402 (ADR 0017).
    [Theory]
    [InlineData("anthropic/claude-sonnet-5", 32_000)]
    [InlineData("openai/gpt-6-luna", 32_000)]
    [InlineData("x-ai/grok-4.7", 32_000)]
    [InlineData("deepseek/deepseek-v4.1-flash", 32_000)]
    [InlineData("qwen/qwen3-coder-plus", null)]
    public void SupportedAndClaude_GetAnswerLimit_ByDefault(string model, int? expected) =>
        Assert.Equal(expected, ModelProfiles.For(model).DefaultMaxOutputTokens);

    [Fact]
    public async Task TruncatedAnswer_ContinuesInSameMessage()
    {
        _fixture.Client.ReplyTruncated("Начало ").Reply("и конец.");

        await _fixture.SendAsync("длинный ответ");

        Assert.Equal(2, _fixture.Client.Requests.Count);
        Assert.Equal(PromptSections.EditorNote(AgentTurn.ContinueAfterLength), _fixture.Client.Requests[1][^1].Question());
        var answer = Assert.Single(Chat.Messages, message => message.Kind == ChatMessageKind.Assistant);
        Assert.Equal("Начало и конец.", answer.Text);
    }

    [Fact]
    public async Task ModePicker_WritesSetting_AndPromptFollows()
    {
        Chat.Mode.PickModeCommand.Execute(null);

        // Deep mode is hidden from the picker pending review but stays in the code.
        Assert.Equal(["Агент", "Вопрос", "План"], _fixture.QuickPick.Items.Select(item => item.Title));
        Assert.StartsWith("текущий", _fixture.QuickPick.Items[0].Detail, StringComparison.Ordinal);

        await _fixture.QuickPick.PickAsync("Вопрос");

        Assert.Equal("ask", _fixture.Settings.Written[AgentModeViewModel.ModeKey]);
        Assert.Equal("Вопрос", Chat.Mode.Title);
    }

    [Fact]
    public async Task ExhaustedBudget_ToolsDoNotRun()
    {
        var budget = new TurnBudget();
        budget.Begin();
        while (budget.TryStartRequest())
        {
        }

        using var writeLock = new SemaphoreSlim(1, 1);
        var guarded = new GuardedToolFunction(AIFunctionFactory.Create(() => "выполнено", "probe"), budget, writeLock, NullLogger.Instance);

        Assert.Equal(GuardedToolFunction.ExhaustedResult, await guarded.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken));
    }

    private static Dictionary<string, object?> Path(int index) => new() { ["path"] = $"f{index}" };

    // Each call waits for the other to start, so sequential execution would never meet.
    private async Task<string> WaitPairAsync(string name)
    {
        var (mine, other) = name == "a" ? (_aStarted, _bStarted) : (_bStarted, _aStarted);
        mine.TrySetResult();
        await other.Task.WaitAsync(PairTimeout);
        return $"встретились {name}";
    }
}
