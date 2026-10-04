using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Chat;

public sealed class ChatViewModelTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    private ChatViewModel Chat => _fixture.Chat;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Send_StreamsAnswer_AndKeepsHistory()
    {
        _fixture.Client.Reply("Привет", ", чем помочь?").Reply("Второй ответ");

        await _fixture.SendAsync("  привет  ");

        Assert.Equal(
            [(ChatMessageKind.User, "привет"), (ChatMessageKind.Assistant, "Привет, чем помочь?")],
            Chat.Messages.Select(message => (message.Kind, message.Text)));
        Assert.False(Chat.IsBusy);
        Assert.Equal(string.Empty, Chat.Input);
        Assert.False(Chat.Messages[1].IsInProgress);

        await _fixture.SendAsync("ещё");

        // The session history reaches the model: the previous question and answer are in the request.
        var second = _fixture.Client.Requests[1];
        Assert.Contains(second, message => message.Role == ChatRole.User && message.Question() == "привет");
        Assert.Contains(second, message => message.Role == ChatRole.Assistant && message.Text == "Привет, чем помочь?");
        Assert.Equal(1, _fixture.ClientsCreated);
    }

    // Empty input isn't sent; during a turn sending stays enabled and the message is queued (ADR 0026).
    [Fact]
    public async Task Send_IsDisabledForEmptyInput_OpenWhileBusy()
    {
        Assert.False(Chat.SendCommand.CanExecute(null));
        Chat.Input = "   ";
        Assert.False(Chat.SendCommand.CanExecute(null));

        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client.Reply("a", "b");
        Chat.Input = "вопрос";
        var sending = Chat.SendCommand.ExecuteAsync(null);
        await Wait.UntilAsync(() => Chat.Messages.Count == 2 && Chat.Messages[1].Text == "a");

        Chat.Input = "следующий";
        Assert.True(Chat.IsBusy);
        Assert.True(Chat.SendCommand.CanExecute(null));
        Assert.True(Chat.StopCommand.CanExecute(null));

        _fixture.Client.Gate.SetResult();
        await sending;
        Assert.True(Chat.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task Stop_CancelsStream_AndMarksAnswer()
    {
        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client.Reply("начало", "никогда");
        Chat.Input = "длинный ответ";
        var sending = Chat.SendCommand.ExecuteAsync(null);
        await Wait.UntilAsync(() => Chat.Messages.Count == 2 && Chat.Messages[1].Text == "начало");

        Chat.StopCommand.Execute(null);
        await sending;

        Assert.Equal("начало …(остановлено)", Chat.Messages[1].Text);
        Assert.False(Chat.IsBusy);
    }

    [Fact]
    public async Task MissingKey_ShowsHint()
    {
        _fixture.ApiKey.Clear();

        await _fixture.SendAsync("привет");

        Assert.False(Chat.HasApiKey);
        var error = Chat.Messages[^1];
        Assert.Equal(ChatMessageKind.Error, error.Kind);
        Assert.StartsWith("Не задан ключ API", error.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServiceErrors_AreExplained()
    {
        _fixture.Client.Fail(new ClientResultException("unauthorized", new StatusResponse(401)));

        await _fixture.SendAsync("привет");

        Assert.Equal("Ключ API не принят. Проверьте его командой «Агент: задать ключ API».", Chat.Messages[^1].Text);
        Assert.Equal(2, Chat.Messages.Count);
    }

    [Fact]
    public async Task ToolCalls_AreShownBeforeAnswer()
    {
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create((string path) => "содержимое " + path, "read_file"));
        _fixture.Client
            .CallTool("c1", "read_file", new Dictionary<string, object?> { ["path"] = "src/Program.cs" })
            .Reply("Файл прочитан.");

        await _fixture.SendAsync("что в Program.cs?");

        Assert.Equal(
            [ChatMessageKind.User, ChatMessageKind.Tool, ChatMessageKind.Assistant],
            Chat.Messages.Select(message => message.Kind));
        Assert.Equal("read_file(path: src/Program.cs)", Chat.Messages[1].Text);
        Assert.False(Chat.Messages[1].IsInProgress);
        Assert.Contains(_fixture.Client.Requests[1], message => message.Contents.OfType<FunctionResultContent>().Any(result => Equals(result.Result?.ToString(), "содержимое src/Program.cs")));
    }

    [Fact]
    public async Task NewChat_ForgetsHistory()
    {
        _fixture.Client.Reply("один").Reply("два");
        await _fixture.SendAsync("первый");

        Chat.NewChatCommand.Execute(null);
        await _fixture.SendAsync("второй");

        Assert.Equal(2, Chat.Messages.Count);
        Assert.DoesNotContain(_fixture.Client.Requests[1], message => message.Question() == "первый");
    }

    [Fact]
    public async Task KeyAndSettingsChanges_RecreateClient_KeepingHistory()
    {
        var changes = new List<string?>();
        ((INotifyPropertyChanged)Chat).PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        _fixture.Client.Reply("один").Reply("два").Reply("три");
        await _fixture.SendAsync("первый");

        _fixture.ApiKey.Set("sk-new");

        Assert.Contains(nameof(ChatViewModel.HasApiKey), changes);
        Assert.True(_fixture.Client.IsDisposed);
        await _fixture.SendAsync("второй");
        Assert.Equal(2, _fixture.ClientsCreated);
        Assert.Contains(_fixture.Client.Requests[1], message => message.Question() == "первый");

        _fixture.Options.Set(new AgentOptions { Model = "openai/gpt-5-mini" });
        await _fixture.SendAsync("третий");
        Assert.Equal(3, _fixture.ClientsCreated);
    }

    // The mode is read on every turn, so switching it neither rebuilds the client nor cancels the cache warmup.
    [Fact]
    public async Task ModeSwitch_KeepsTheClient()
    {
        _fixture.Client.Reply("один").Reply("два");
        await _fixture.SendAsync("первый");

        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, AutoMemory = false, Approvals = AgentApprovals.Edits, Mode = AgentMode.Ask });
        await _fixture.SendAsync("второй");

        Assert.Equal(1, _fixture.ClientsCreated);
        Assert.False(_fixture.Client.IsDisposed);
    }

    private sealed class StatusResponse(int status) : PipelineResponse
    {
        public override int Status { get; } = status;

        public override string ReasonPhrase => "status";

        public override Stream? ContentStream { get; set; }

        public override BinaryData Content => BinaryData.FromString("{}");

        protected override PipelineResponseHeaders HeadersCore => throw new NotSupportedException();

        public override BinaryData BufferContent(CancellationToken cancellationToken = default) => Content;

        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Content);

        public override void Dispose()
        {
        }
    }
}
