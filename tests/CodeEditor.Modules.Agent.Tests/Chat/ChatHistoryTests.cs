using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Chat;

public sealed class ChatHistoryTests : IDisposable
{
    private static readonly string Folder = Path.Combine(AgentFixture.Root, ".breeze", "agent");

    private readonly AgentFixture _fixture = new();

    public ChatHistoryTests() => _fixture.Workspace.Open(AgentFixture.Root);

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task AnswerIsSaved_WithGitIgnore()
    {
        _fixture.Client.Reply("Привет!");

        await _fixture.SendAsync("здравствуй");

        Assert.Equal("*\n", _fixture.FileSystem.ReadAllText(Path.Combine(Folder, ".gitignore")));
        var chat = Assert.Single(ChatFiles());
        var text = _fixture.FileSystem.ReadAllText(chat);
        Assert.Contains("\"title\": \"здравствуй\"", text, StringComparison.Ordinal);
        Assert.Contains("Привет!", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restart_RestoresFeedAndModelMemory()
    {
        _fixture.Client.Reply("Меня зовут Агент.").Reply("Вы спрашивали, как меня зовут.");
        await _fixture.SendAsync("как тебя зовут?");

        // "Restart": a new feed over the same files and conversation.
        _fixture.Conversation.Reset();
        using var restarted = _fixture.CreateChat(_fixture.CreateHistory());

        Assert.Equal(["как тебя зовут?", "Меня зовут Агент."], restarted.Messages.Select(message => message.Text));

        restarted.Input = "что я спрашивал?";
        await restarted.SendCommand.ExecuteAsync(null);

        Assert.Contains(_fixture.Client.Requests[1], message => message.Role == ChatRole.User && message.Question() == "как тебя зовут?");
        Assert.Single(ChatFiles());
    }

    [Fact]
    public async Task NewChat_GoesToNewFile()
    {
        _fixture.Client.Reply("один").Reply("два");
        await _fixture.SendAsync("первый");

        _fixture.Chat.NewChatCommand.Execute(null);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        await _fixture.SendAsync("второй");

        Assert.Equal(2, ChatFiles().Count);
    }

    [Fact]
    public async Task OtherFolder_HasOwnHistory()
    {
        _fixture.Client.Reply("ответ");
        await _fixture.SendAsync("вопрос");
        var other = Path.GetFullPath(@"C:\other");
        _fixture.FileSystem.AddDirectory(other);

        _fixture.Workspace.Open(other);

        Assert.Empty(_fixture.Chat.Messages);

        _fixture.Workspace.Open(AgentFixture.Root);

        Assert.Equal(2, _fixture.Chat.Messages.Count);
    }

    // A new chat started while a turn runs: the stopped turn is saved into its own chat, and nothing of it (the stop
    // mark, an error) reaches the new one. It used to be lost, with its tail written into the next chat.
    [Fact]
    public async Task NewChatDuringTurn_SavesTheStoppedTurnIntoItsOwnChat()
    {
        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client.Reply("начало", "никогда");
        _fixture.Chat.Input = "длинный вопрос";
        var sending = _fixture.Chat.SendCommand.ExecuteAsync(null);
        await Wait.UntilAsync(() => _fixture.Chat.Messages.Count == 2 && _fixture.Chat.Messages[1].Text == "начало");

        await _fixture.Chat.NewChatCommand.ExecuteAsync(null);
        await sending;

        Assert.Empty(_fixture.Chat.Messages);
        Assert.Null(_fixture.Chat.Session.CurrentId);
        Assert.Single(ChatFiles());
        var saved = _fixture.CreateChat(_fixture.CreateHistory());
        await saved.Session.LoadLatestAsync();
        Assert.Equal(
            [(ChatMessageKind.User, "длинный вопрос"), (ChatMessageKind.Assistant, "начало …(остановлено)")],
            saved.Messages.Select(message => (message.Kind, message.Text)));
    }

    // Another folder opened while a turn runs: the turn stops, and its chat is not written into the new folder.
    [Fact]
    public async Task OtherFolderDuringTurn_DoesNotSaveTheTurnIntoTheNewFolder()
    {
        var other = Path.GetFullPath(@"C:\other");
        _fixture.FileSystem.AddDirectory(other);
        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client.Reply("начало", "никогда");
        _fixture.Chat.Input = "длинный вопрос";
        var sending = _fixture.Chat.SendCommand.ExecuteAsync(null);
        await Wait.UntilAsync(() => _fixture.Chat.Messages.Count == 2 && _fixture.Chat.Messages[1].Text == "начало");

        _fixture.Workspace.Open(other);
        await sending;

        // The folder's chat opens right after the turn has wound down.
        await Wait.UntilAsync(() => _fixture.Chat.Messages.Count == 0);
        Assert.False(_fixture.Chat.IsBusy);
        Assert.False(_fixture.FileSystem.DirectoryExists(Path.Combine(other, ".breeze", "agent")));
    }

    private List<string> ChatFiles() =>
        [.. _fixture.FileSystem.EnumerateEntries(Folder).Where(entry => entry.Name.StartsWith("chat-", StringComparison.Ordinal)).Select(entry => entry.FullPath)];
}
