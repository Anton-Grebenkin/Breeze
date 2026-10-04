using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Services.Chat;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Tools;

/// <summary>Workflow tools: the task plan, questions to the user, the diff of chat edits.</summary>
public sealed class WorkflowToolsTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private readonly ChatChanges _changes;

    public WorkflowToolsTests()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _changes = new ChatChanges(_fixture.FileState, _fixture.Documents, _fixture.FileSystem, _fixture.Workspace, new InlineUiDispatcher());
        _fixture.ToolProviders.Add(new WorkflowAgentTools(_fixture.Todos, _fixture.Questions, _changes, _fixture.Outputs));
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ManageTodo_ReplacesPlan_ShowsPanel_AndContext()
    {
        var result = await Invoke(WorkflowAgentTools.ManageTodoName, new() { ["items"] = Items(("1", "Найти место", "completed"), ("2", "Исправить баг", "in_progress"), ("3", "Прогнать тесты", "pending")) });

        Assert.Equal("План обновлён:\n[x] 1. Найти место\n[>] 2. Исправить баг\n[ ] 3. Прогнать тесты", result);
        Assert.Equal("План · 1 из 3", _fixture.Chat.Todo.Title);
        Assert.True(_fixture.Chat.Todo.HasItems);
        var context = await new TodoAgentContext(_fixture.Todos).GetContextAsync(new AgentContextRequest(false), TestContext.Current.CancellationToken);
        Assert.StartsWith("Текущий план (manage_todo):\n[x] 1.", Assert.Single(context), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManageTodo_Validates_AndWarns()
    {
        Assert.Contains("только один пункт", (await Assert.ThrowsAsync<AgentToolException>(() =>
            Invoke(WorkflowAgentTools.ManageTodoName, new() { ["items"] = Items(("1", "а", "in_progress"), ("2", "б", "in_progress")) }))).Message, StringComparison.Ordinal);
        Assert.Contains("неизвестное состояние", (await Assert.ThrowsAsync<AgentToolException>(() =>
            Invoke(WorkflowAgentTools.ManageTodoName, new() { ["items"] = Items(("1", "а", "maybe")) }))).Message, StringComparison.Ordinal);

        Assert.EndsWith("план обычно не нужен.)", await Invoke(WorkflowAgentTools.ManageTodoName, new() { ["items"] = Items(("1", "а", "pending")) }), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewChat_ClearsPlan()
    {
        await Invoke(WorkflowAgentTools.ManageTodoName, new() { ["items"] = Items(("1", "а", "pending"), ("2", "б", "pending"), ("3", "в", "pending")) });

        _fixture.Chat.NewChatCommand.Execute(null);

        Assert.False(_fixture.Chat.Todo.HasItems);
    }

    [Fact]
    public async Task AskUser_ShowsCard_AndReturnsChosenOption()
    {
        _fixture.Client
            .CallTool("q1", WorkflowAgentTools.AskUserName, new Dictionary<string, object?> { ["question"] = "Какой логгер?", ["options"] = new[] { "Serilog", "Встроенный" } })
            .Reply("Беру встроенный.");
        _fixture.Questions.Asked += (_, card) => card.ChooseCommand.Execute("Встроенный");

        await _fixture.SendAsync("добавь логирование");

        var question = Assert.Single(_fixture.Chat.Messages, message => message.Kind == ChatMessageKind.Question);
        Assert.Equal("Какой логгер?\n→ Встроенный", question.Text);
        var result = _fixture.Client.Requests[^1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single();
        Assert.Equal("Пользователь ответил: Встроенный", result.Result?.ToString());
    }

    [Fact]
    public async Task AskUser_Stop_RemovesQuestion()
    {
        _fixture.Client.CallTool("q1", WorkflowAgentTools.AskUserName, new Dictionary<string, object?> { ["question"] = "Продолжать?" });
        _fixture.Questions.Asked += (_, _) => _fixture.Chat.StopCommand.Execute(null);

        await _fixture.SendAsync("задача");

        var question = Assert.Single(_fixture.Chat.Messages, message => message.Kind == ChatMessageKind.Question);
        // The mark is added by the question's continuation on the thread pool, sometimes after the turn ends.
        await Wait.UntilAsync(() => question.Text.EndsWith("(вопрос снят)", StringComparison.Ordinal));
        Assert.False(question.Question!.IsPending);
    }

    [Fact]
    public async Task GetChanges_DiffsAgainstTextBeforeFirstEdit()
    {
        var path = Path.Combine(AgentFixture.Root, "a.cs");
        var created = Path.Combine(AgentFixture.Root, "b.cs");
        _fixture.FileSystem.AddFile(path, "one\ntwo\nthree\n").AddFile(created, "new\n");
        _fixture.FileState.RecordWrite(path, "one\n2\nthree\n", "one\ntwo\nthree\n");
        _fixture.FileSystem.AddFile(path, "one\n2\nthree\n");
        _fixture.FileState.RecordWrite(created, "new\n", previousText: null);

        var diff = await Invoke(WorkflowAgentTools.GetChangesName, []);

        Assert.Contains("--- a/a.cs\n+++ b/a.cs\n@@ -1,3 +1,3 @@\n one\n-two\n+2\n three\n", diff, StringComparison.Ordinal);
        Assert.Contains("--- /dev/null\n+++ b/b.cs", diff, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetChanges_WithoutEdits_SaysSo() =>
        Assert.StartsWith("В этом чате вы ещё ничего не меняли", await Invoke(WorkflowAgentTools.GetChangesName, []), StringComparison.Ordinal);

    private static TodoItemInput[] Items(params (string Id, string Title, string Status)[] items) =>
        [.. items.Select(item => new TodoItemInput(item.Id, item.Title, item.Status))];

    private async Task<string> Invoke(string tool, Dictionary<string, object?> arguments)
    {
        var function = new WorkflowAgentTools(_fixture.Todos, _fixture.Questions, _changes, _fixture.Outputs).CreateTools().OfType<AIFunction>().Single(candidate => candidate.Name == tool);
        return (await function.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;
    }
}
