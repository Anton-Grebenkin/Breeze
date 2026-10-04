using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>Agent log: model requests, turns and errors, but never conversation text.</summary>
public sealed class AgentLoggingTests : IDisposable
{
    private const string Secret = "секретный-вопрос-про-код";
    private const string Answer = "секретный-ответ-модели";

    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ModelCall_IsLoggedWithTokens_AndAgentSettings()
    {
        _fixture.Options.Set(new AgentOptions { Model = "test/model", Endpoint = "https://api.example.com/v1?key=abc", Temperature = 0.5 });
        _fixture.Client.ReplyWithUsage(Answer, inputTokens: 1_200, outputTokens: 34);

        await _fixture.SendAsync(Secret);

        var messages = _fixture.ConversationLog.Entries.Select(entry => entry.Message).ToList();
        // Without modules the only tool is the explore subagent.
        Assert.Contains(messages, message => message.StartsWith("Agent ready: model test/model, service api.example.com, tools 1, temperature 0.5", StringComparison.Ordinal));
        var call = Assert.Single(messages, message => message.StartsWith("Model test/model", StringComparison.Ordinal));
        Assert.Contains("tokens: input 1200, output 34", call, StringComparison.Ordinal);
        Assert.DoesNotContain(messages, message => message.Contains("key=abc", StringComparison.Ordinal));

        var turn = _fixture.ActivityLog.Entries.Select(entry => entry.Message).ToList();
        Assert.Equal(2, turn.Count);
        Assert.Equal($"Agent turn: chat new, question {Secret.Length} chars, active file: none, attached files: 0", turn[0]);
        Assert.StartsWith("Agent turn finished in", turn[1], StringComparison.Ordinal);
        AssertNoConversationText();
    }

    [Fact]
    public async Task ServiceError_IsLoggedWithException()
    {
        _fixture.Client.Fail(new HttpRequestException("503 Service Unavailable"));

        await _fixture.SendAsync(Secret);

        var failed = Assert.Single(_fixture.ConversationLog.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("request failed", failed.Message, StringComparison.Ordinal);
        Assert.IsType<HttpRequestException>(failed.Exception);
        Assert.Contains(_fixture.ActivityLog.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("(HttpRequestException)", StringComparison.Ordinal));
        AssertNoConversationText();
    }

    [Fact]
    public async Task Stop_IsLoggedAsStopped()
    {
        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client.Reply("начало", "никогда");
        _fixture.Chat.Input = Secret;
        var sending = _fixture.Chat.SendCommand.ExecuteAsync(null);
        for (var i = 0; i < 200 && _fixture.Chat.Messages.Count < 2; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        _fixture.Chat.StopCommand.Execute(null);
        await sending;

        Assert.Contains(_fixture.ConversationLog.Entries, entry => entry.Message.Contains("request stopped", StringComparison.Ordinal));
        Assert.Contains(_fixture.ActivityLog.Entries, entry => entry.Message.StartsWith("Agent turn stopped by the user", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChatActions_AreLogged()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.Client.Reply("ответ");
        await _fixture.SendAsync(Secret);
        var id = _fixture.Chat.Session.CurrentId;

        _fixture.Chat.NewChatCommand.Execute(null);
        await _fixture.Chat.OpenChatCommand.ExecuteAsync(id);
        _fixture.Chat.DeleteChatCommand.Execute(null);

        var messages = _fixture.ActivityLog.Entries.Select(entry => entry.Message).ToList();
        Assert.Contains("New chat", messages);
        Assert.Contains($"Opened chat {id}", messages);
        Assert.Contains($"Deleted chat {id} (to the recycle bin)", messages);
    }

    private void AssertNoConversationText()
    {
        var all = _fixture.ConversationLog.Entries.Select(entry => entry.Message).Concat(_fixture.ActivityLog.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain(all, message => message.Contains(Secret, StringComparison.Ordinal) || message.Contains(Answer, StringComparison.Ordinal));
    }
}
