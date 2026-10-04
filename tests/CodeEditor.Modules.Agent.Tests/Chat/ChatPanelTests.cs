using CodeEditor.Modules.Agent.Services.History;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Chat;

public sealed class ChatPanelTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public ChatPanelTests() => _fixture.Workspace.Open(AgentFixture.Root);

    private ChatViewModel Chat => _fixture.Chat;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Context_ShowsReportedUsage_AndIsSavedWithChat()
    {
        _fixture.Options.Set(new AgentOptions { ContextWindow = 10_000 });
        _fixture.Client.ReplyWithUsage("ответ", inputTokens: 7_000, outputTokens: 1_500);

        await _fixture.SendAsync("вопрос");

        Assert.Equal("85%", Chat.Context.PercentText);
        Assert.True(Chat.Context.IsNearlyFull);
        Assert.Contains("8,5 тыс. из 10 тыс.", Chat.Context.Summary, StringComparison.Ordinal);

        Chat.NewChatCommand.Execute(null);
        Assert.Equal("0%", Chat.Context.PercentText);

        await Chat.OpenChatCommand.ExecuteAsync(Assert.Single(_fixture.History.List()).Id);
        Assert.Equal("85%", Chat.Context.PercentText);
    }

    [Fact]
    public async Task Context_IsEstimated_WhenServiceSendsNoUsage()
    {
        _fixture.Client.Reply(new string('а', 4_000));

        await _fixture.SendAsync("вопрос");

        Assert.True(Chat.Context.Usage.IsEstimated);
        Assert.StartsWith("≈", Chat.Context.PercentText, StringComparison.Ordinal);
        Assert.True(Chat.Context.Usage.ContextTokens >= 1_000);
    }

    [Fact]
    public async Task History_ListsChats_OpensAndDeletes()
    {
        _fixture.Client.Reply("один").Reply("два");
        await _fixture.SendAsync("первый вопрос");
        Chat.NewChatCommand.Execute(null);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        await _fixture.SendAsync("второй вопрос");

        Chat.ShowHistoryCommand.Execute(null);
        Assert.Equal(["второй вопрос", "первый вопрос"], _fixture.QuickPick.Items.Select(item => item.Title));
        Assert.EndsWith("открыт", _fixture.QuickPick.Items[0].Detail, StringComparison.Ordinal);

        await _fixture.QuickPick.PickAsync("первый");
        Assert.Equal("первый вопрос", Chat.Title);
        Assert.Equal(["первый вопрос", "один"], Chat.Messages.Select(message => message.Text));

        Chat.DeleteChatCommand.Execute(null);
        Assert.Empty(Chat.Messages);
        Assert.Equal(ChatHistory.NewChatTitle, Chat.Title);
        Assert.Equal(["второй вопрос"], _fixture.History.List().Select(chat => chat.Title));
    }

    [Fact]
    public async Task RecentChats_ExcludeCurrent_AndWelcomeShowsForEmptyChat()
    {
        _fixture.Client.Reply("один");
        await _fixture.SendAsync("первый вопрос");
        Assert.False(Chat.IsWelcomeVisible);
        Assert.False(Chat.HasRecentChats);

        Chat.NewChatCommand.Execute(null);

        Assert.True(Chat.IsWelcomeVisible);
        var recent = Assert.Single(Chat.RecentChats);
        Assert.Equal("первый вопрос", recent.Title);
        Assert.StartsWith("сегодня", recent.When, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenChat_ThatWasDeleted_KeepsCurrentAndRefreshesList()
    {
        _fixture.Client.Reply("один");
        await _fixture.SendAsync("первый вопрос");
        var id = Assert.Single(_fixture.History.List()).Id;
        Chat.NewChatCommand.Execute(null);
        new ChatHistoryStore(_fixture.Workspace, _fixture.FileSystem, Microsoft.Extensions.Logging.Abstractions.NullLogger<ChatHistoryStore>.Instance).Delete(id);

        await Chat.OpenChatCommand.ExecuteAsync(id);

        Assert.Empty(Chat.Messages);
        Assert.Empty(Chat.RecentChats);
    }

    [Fact]
    public async Task Message_CarriesContextBlock_FromProviders()
    {
        var provider = new RecordingContextProvider("Открыт файл src/App.cs, курсор в строке 12.");
        _fixture.ContextProviders.Add(new FailingContextProvider());
        _fixture.ContextProviders.Add(provider);
        _fixture.Client.Reply("один").Reply("два");

        await _fixture.SendAsync("вопрос");
        Chat.ActiveFile.ToggleCommand.Execute(null);
        await _fixture.SendAsync("ещё");

        var contents = _fixture.Client.Requests[0][^1].Contents.OfType<TextContent>().Select(content => content.Text).ToList();
        var context = contents.Single(text => text.StartsWith("<context>", StringComparison.Ordinal));
        Assert.Equal("вопрос", contents[0]);
        Assert.StartsWith("<context>\nDate: ", context, StringComparison.Ordinal);
        Assert.Contains("Открыт файл src/App.cs, курсор в строке 12.", context, StringComparison.Ordinal);
        Assert.Equal([true, false], provider.Requests.Select(request => request.IncludeActiveEditor));
        Assert.Null(Chat.Messages[0].Attachment);
    }

    [Fact]
    public async Task CopyLastAnswer_PutsMarkdownToClipboard()
    {
        _fixture.Client.Reply("**жирный** и `код`");
        await _fixture.SendAsync("вопрос");

        Chat.CopyLastAnswerCommand.Execute(null);

        Assert.Equal("**жирный** и `код`", _fixture.Shell.Clipboard);
    }

    [Fact]
    public async Task Parameters_ReachTheModel()
    {
        _fixture.Options.Set(new AgentOptions { Model = "deepseek/deepseek-v4-flash", Temperature = 0.2, MaxOutputTokens = 4_096, ReasoningEffort = AgentReasoningEffort.Low });
        _fixture.Client.Reply("ответ");

        await _fixture.SendAsync("вопрос");

        var options = _fixture.Client.LastOptions!;
        Assert.Equal(0.2f, options.Temperature);
        Assert.Equal(4_096, options.MaxOutputTokens);
        Assert.Equal(Microsoft.Extensions.AI.ReasoningEffort.Low, options.Reasoning?.Effort);
    }
}
