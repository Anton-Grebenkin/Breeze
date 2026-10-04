using System.Text;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Approvals;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>"Accept edits immediately" mode and the changed files panel: accept, revert a file, revert all.</summary>
public sealed class AcceptEditsTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private readonly string _path = Path.Combine(AgentFixture.Root, "a.cs");

    public AcceptEditsTests()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.FileSystem.AddFile(_path, "void Run() { }\n");
        _fixture.ToolProviders.Add(new FileTools(_fixture));
    }

    private ChatViewModel Chat => _fixture.Chat;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task AutoMode_AppliesEditWithoutCard_AndShowsChangedFile()
    {
        AcceptEdits();
        _fixture.Client.CallTool("c1", "apply_edits", Args("a.cs")).Reply("Готово.");

        await Send("переименуй");

        Assert.DoesNotContain(Chat.Messages, message => message.Approval is not null);
        Assert.Equal("void Execute() { }\n", _fixture.FileSystem.ReadAllText(_path));
        var file = Assert.Single(Chat.Changes.Files);
        Assert.Equal(("a.cs", "+1 −1"), (file.Name, file.Diff.Counts));
        Assert.Equal(("Изменено файлов: 1", "+1 −1"), (Chat.Changes.Title, Chat.Changes.Counts));
    }

    [Fact]
    public async Task TextBeforeEditNeedingApproval_IsProgress()
    {
        AcceptEdits();
        _fixture.Client.SayAndCallTool("Правлю метод.", "c1", "apply_edits", Args("a.cs")).Reply("Готово.");

        await Send("переименуй");

        Assert.Equal(
            [ChatMessageKind.User, ChatMessageKind.Progress, ChatMessageKind.Tool, ChatMessageKind.Assistant],
            Chat.Messages.Select(message => message.Kind));
    }

    [Fact]
    public async Task AutoMode_StillAsksBeforeDeleting()
    {
        AcceptEdits();
        _fixture.Client.CallTool("c1", "delete_file", Args("a.cs")).Reply("Удалил.");

        var sending = Send("удали");
        var card = await WaitForCardAsync();
        Assert.Equal(ApprovalState.Pending, card.State);
        card.RejectCommand.Execute(null);
        await sending;

        Assert.True(_fixture.FileSystem.FileExists(_path));
    }

    [Fact]
    public async Task EditsMode_AsksForEachEdit()
    {
        _fixture.Client.CallTool("c1", "apply_edits", Args("a.cs")).Reply("Готово.");

        var sending = Send("переименуй");
        (await WaitForCardAsync()).ApproveCommand.Execute(null);
        await sending;

        Assert.Single(Chat.Changes.Files);
    }

    [Fact]
    public async Task RevertFile_RestoresOriginal_AgentMustReadAgain()
    {
        await EditInAutoModeAsync();

        await Chat.Changes.RevertFileCommand.ExecuteAsync(Chat.Changes.Files[0]);

        Assert.Equal([_path], _fixture.Reverter.Reverted);
        Assert.Equal("void Run() { }\n", _fixture.FileSystem.ReadAllText(_path));
        Assert.False(Chat.Changes.HasFiles);
        Assert.NotNull(_fixture.FileState.CheckEditable(_path, "a.cs", "void Run() { }\n"));
    }

    [Fact]
    public async Task RevertAll_RemovesCreatedFile()
    {
        var created = Path.Combine(AgentFixture.Root, "b.cs");
        AcceptEdits();
        _fixture.Client.CallTools(("c1", "apply_edits", Args("a.cs")), ("c2", "create_file", Args("b.cs"))).Reply("Готово.");
        await Send("сделай");
        Assert.Equal(2, Chat.Changes.Files.Count);
        Assert.Equal("новый", Chat.Changes.Files.Single(file => file.Name == "b.cs").Status);

        await Chat.Changes.RevertAllCommand.ExecuteAsync(null);

        Assert.Equal("void Run() { }\n", _fixture.FileSystem.ReadAllText(_path));
        Assert.False(_fixture.FileSystem.FileExists(created));
        Assert.Empty(Chat.Changes.Files);
    }

    [Fact]
    public async Task Accept_ClearsListAndChatDiff()
    {
        await EditInAutoModeAsync();

        await Chat.Changes.AcceptCommand.ExecuteAsync(null);

        Assert.Empty(Chat.Changes.Files);
        Assert.Empty(_fixture.FileState.Changes);
        Assert.Equal("void Execute() { }\n", _fixture.FileSystem.ReadAllText(_path));
        Assert.Equal("Правки агента приняты: 1 файл", _fixture.StatusBar.Message);
    }

    // Edits awaiting review outlive the chat, as in Cursor: they stay until accepted or rejected (ADR 0040).
    [Fact]
    public async Task NewChat_KeepsChangedFilesForReview()
    {
        await EditInAutoModeAsync();

        Chat.NewChatCommand.Execute(null);

        Assert.Single(Chat.Changes.Files);
    }

    [Fact]
    public async Task RevertCommands_DisabledWhileAgentWorks()
    {
        await EditInAutoModeAsync();
        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client.Reply("начало", "конец");

        var sending = Send("ещё");
        Assert.False(Chat.Changes.RevertAllCommand.CanExecute(null));
        _fixture.Client.Gate.SetResult();
        await sending;

        Assert.True(Chat.Changes.RevertAllCommand.CanExecute(null));
    }

    private async Task EditInAutoModeAsync()
    {
        AcceptEdits();
        _fixture.Client.CallTool("c1", "apply_edits", Args("a.cs")).Reply("Готово.");
        await Send("переименуй");
    }

    private void AcceptEdits() => _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, Approvals = AgentApprovals.Auto });

    private static Dictionary<string, object?> Args(string path) => new() { ["path"] = path };

    private Task Send(string text)
    {
        Chat.Input = text;
        return Chat.SendCommand.ExecuteAsync(null);
    }

    private async Task<ApprovalCardViewModel> WaitForCardAsync()
    {
        for (var i = 0; i < 200; i++)
        {
            if (Chat.Messages.LastOrDefault(message => message.Approval is { IsPending: true })?.Approval is { } card)
            {
                return card;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Карточка подтверждения не появилась.");
    }

    /// <summary>
    /// Edit (Run → Execute), create and delete tools that record into the chat file state like the real ones.
    /// </summary>
    private sealed class FileTools(AgentFixture fixture) : IAgentToolProvider
    {
        public IEnumerable<AITool> CreateTools() =>
        [
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create(Edit, "apply_edits")),
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create(Create, "create_file")),
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create(Delete, "delete_file")),
        ];

        private string Edit(string path)
        {
            var full = Full(path);
            var old = fixture.FileSystem.ReadAllText(full);
            var text = old.Replace("Run", "Execute", StringComparison.Ordinal);
            Write(full, text);
            fixture.FileState.RecordWrite(full, text, old);
            return "ok";
        }

        private string Create(string path)
        {
            Write(Full(path), "class B { }\n");
            fixture.FileState.RecordWrite(Full(path), "class B { }\n", previousText: null);
            return "ok";
        }

        private string Delete(string path)
        {
            fixture.FileSystem.DeleteToRecycleBin(Full(path));
            return "ok";
        }

        private void Write(string path, string text) => fixture.FileSystem.WriteAllBytesAtomic(path, Encoding.UTF8.GetBytes(text));

        private static string Full(string path) => Path.Combine(AgentFixture.Root, path);
    }
}
