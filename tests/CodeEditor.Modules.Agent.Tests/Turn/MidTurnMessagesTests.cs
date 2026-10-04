using CodeEditor.Modules.Agent.Services.Chat;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>
/// Messages sent mid-turn (ADR 0026): the input stays enabled, the message waits for a step boundary (the end of the
/// current tool calls or answer) and goes to the model as a regular user message; stopping returns it to the input.
/// </summary>
public sealed class MidTurnMessagesTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    private ChatViewModel Chat => _fixture.Chat;

    public void Dispose() => _fixture.Dispose();

    // A message written while a tool runs goes into the next model request, right after the tool result.
    [Fact]
    public async Task MessageDuringTool_ReachesTheModelBeforeTheNextStep()
    {
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create(() =>
        {
            _fixture.Queue.Enqueue(Queued("и добавь тест"));
            return "прочитано";
        }, "slow_read"));
        _fixture.Client.CallTool("c1", "slow_read", new Dictionary<string, object?>()).Reply("Учёл: добавлю тест.");

        await _fixture.SendAsync("поправь ошибку");

        var second = _fixture.Client.Requests[1];
        Assert.Equal("и добавь тест", second[^1].Text);
        Assert.Contains(second[^2].Contents, content => content is FunctionResultContent { CallId: "c1" });
        Assert.Equal(["поправь ошибку", "и добавь тест"], Questions());
        Assert.Equal("Учёл: добавлю тест.", Chat.Messages.Last(message => message.Kind == ChatMessageKind.Assistant).Text);
    }

    [Fact]
    public async Task SendWhileAnswering_IsQueued_ThenAnsweredInTheSameTurn()
    {
        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client.Reply("Первый ", "ответ.").Reply("Второй ответ.");
        Chat.Input = "вопрос";
        var sending = Chat.SendCommand.ExecuteAsync(null);
        await Wait.UntilAsync(() => Chat.Messages.Any(message => message.Text.StartsWith("Первый", StringComparison.Ordinal)));

        Chat.Input = "уточнение";
        await Chat.SendCommand.ExecuteAsync(null);
        Assert.Equal("уточнение", Assert.Single(Chat.Queued.Items).Text);
        _fixture.Client.Gate.SetResult();
        await sending;

        Assert.Equal(["вопрос", "уточнение"], Questions());
        Assert.Empty(Chat.Queued.Items);
        Assert.Equal("Второй ответ.", Chat.Messages.Last(message => message.Kind == ChatMessageKind.Assistant).Text);
        Assert.Equal("уточнение", _fixture.Client.Requests[1].Last(message => message.Role == ChatRole.User).Question());
    }

    // On stop, unsent messages go back to the input, before what's already typed.
    [Fact]
    public async Task Stop_ReturnsQueuedTextToInput()
    {
        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client.Reply("начало", "никогда");
        Chat.Input = "вопрос";
        var sending = Chat.SendCommand.ExecuteAsync(null);
        await Wait.UntilAsync(() => Chat.Messages.Count == 2);
        Chat.Input = "уточнение";
        await Chat.SendCommand.ExecuteAsync(null);
        Chat.Input = "набираю";

        Chat.StopCommand.Execute(null);
        await sending;

        Assert.Equal("уточнение" + Environment.NewLine + "набираю", Chat.Input);
        Assert.False(_fixture.Queue.HasPending);
        Assert.Empty(Chat.Queued.Items);
    }

    [Fact]
    public void QueuedMessage_CanBeRemoved()
    {
        _fixture.Queue.Enqueue(Queued("лишнее"));

        Chat.Queued.Items[0].RemoveCommand.Execute(null);

        Assert.False(_fixture.Queue.HasPending);
        Assert.Empty(Chat.Queued.Items);
    }

    private IEnumerable<string> Questions() => Chat.Messages.Where(message => message.Kind == ChatMessageKind.User).Select(message => message.Text);

    private static QueuedMessage Queued(string text) => new(text, new ChatMessage(ChatRole.User, text), [], null);
}
