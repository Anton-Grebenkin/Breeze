using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Services.Context;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Context;

/// <summary>Context compaction in the tool loop: collapsing old results, summarizing, rereading files after.</summary>
public sealed partial class ContextCompactionTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public ContextCompactionTests() =>
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create((int n, int size) => $"результат {n}\n" + new string('x', size), "read_big"));

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void InputBudget_IsWindowMinusAnswerAndReserve()
    {
        Assert.Equal(200_000 - 8_000 - ContextCompaction.ReservedTokens, ContextCompaction.InputBudget(new AgentOptions { ContextWindow = 200_000, MaxOutputTokens = 8_000 }));
        Assert.Equal(ContextCompaction.MinimumInputBudget, ContextCompaction.InputBudget(new AgentOptions { ContextWindow = 10_000 }));
    }

    // A 1M window doesn't mean compacting at 1M: the default chat limit is 200K (ADR 0012).
    [Fact]
    public void InputBudget_IsCappedByChatLimit()
    {
        Assert.Equal(ContextCompaction.DefaultContextLimit, ContextCompaction.InputBudget(new AgentOptions { ContextWindow = 1_000_000 }));
        Assert.Equal(100_000, ContextCompaction.InputBudget(new AgentOptions { ContextWindow = 1_000_000, ContextLimit = 100_000 }));
        Assert.Equal(ContextCompaction.DefaultContextLimit, ContextCompaction.ContextLimit(new AgentOptions { ContextWindow = 1_000_000 }));
        Assert.Equal(128_000, ContextCompaction.ContextLimit(new AgentOptions { ContextWindow = 128_000 }));
    }

    // A summary forgets which files changed, so the harness appends the changed files and the plan.
    [Fact]
    public async Task Summary_CarriesChangedFilesAndPlan_FromTheEditor()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, ContextWindow = 16_000, MaxOutputTokens = 1_000 });
        _fixture.FileState.RecordWrite(Path.Combine(AgentFixture.Root, "src", "A.cs"), "class A { int X; }", "class A { }");
        _fixture.FileState.RecordWrite(Path.Combine(AgentFixture.Root, "src", "B.cs"), "class B { }", previousText: null);
        // The item is done: an open one would add a plan reminder to the turn, and this test is about summaries.
        _fixture.Todos.Replace([new TodoItem("1", "Поправить A", TodoStatus.Completed)]);
        ScriptCalls(count: 5, size: 3_000).Reply("1. Задача: поправить A.").Reply("Готово.");

        await SendAsync();

        var last = Text(_fixture.Client.Requests[^1]);
        Assert.Contains("1. Задача: поправить A.", last, StringComparison.Ordinal);
        Assert.Contains(CompactionPrompts.StateHeader, last, StringComparison.Ordinal);
        Assert.Contains("Files changed in this chat: src/A.cs, src/B.cs (created).", last, StringComparison.Ordinal);
        Assert.Contains("[x] 1. Поправить A", last, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OldToolResults_AreCollapsed_RecentKept()
    {
        // Input budget of 17,000 tokens: collapsing starts at 8,500, i.e. about 34 KB of history.
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, ContextWindow = 30_000, MaxOutputTokens = 1_000 });
        ScriptCalls(count: 12, size: 4_000).Reply("Готово.");

        await SendAsync();

        var last = Text(_fixture.Client.Requests[^1]);
        Assert.Contains(CompactionPrompts.ToolGroupHeader, last, StringComparison.Ordinal);
        Assert.Contains("→ результат 0", last, StringComparison.Ordinal);
        Assert.Contains("результат 11\nxxxx", last, StringComparison.Ordinal);
        Assert.DoesNotContain("результат 0\nxxxx", last, StringComparison.Ordinal);
        Assert.Empty(_fixture.HelperClient.Requests);
    }

    // The model reports a quarter of the framework's estimate (Russian text, reasoning): the raw estimate would collapse
    // at the eighth result, the corrected one says it's too early.
    [Fact]
    public async Task ReportedInput_CorrectsEstimate_CollapseWaits()
    {
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, ContextWindow = 30_000, MaxOutputTokens = 1_000 });
        for (var n = 0; n < 12; n++)
        {
            _fixture.Client.CallToolWithUsage($"c{n}", "read_big", new Dictionary<string, object?> { ["n"] = n, ["size"] = 4_000 }, inputTokens: 500 + (n * 250));
        }

        _fixture.Client.Reply("Готово.");

        await SendAsync();

        Assert.DoesNotContain(CompactionPrompts.ToolGroupHeader, Text(_fixture.Client.Requests[^1]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OverflowingContext_IsSummarized_AndFilesMustBeReadAgain()
    {
        // Input budget of 4,000 tokens: summarizing starts at 3,200, after the fifth ~750-token result.
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, ContextWindow = 16_000, MaxOutputTokens = 1_000 });
        var path = Path.Combine(AgentFixture.Root, "a.cs");
        _fixture.FileState.RecordRead(path, "class A { }", 1, 1);
        ScriptCalls(count: 5, size: 3_000).Reply("1. Задача: проверить большие файлы.").Reply("Готово.");

        await SendAsync();

        Assert.Empty(_fixture.HelperClient.Requests);
        var last = Text(_fixture.Client.Requests[^1]);
        Assert.Contains("1. Задача: проверить большие файлы.", last, StringComparison.Ordinal);
        Assert.DoesNotContain("результат 0\n", last, StringComparison.Ordinal);
        Assert.Contains("результат 4\n", last, StringComparison.Ordinal);
        Assert.Contains(_fixture.Chat.Messages, message => message.Kind == ChatMessageKind.Status && message.Text == ContextCompaction.SummarizedNotice);
        Assert.Contains("после сжатия контекста", _fixture.FileState.CheckEditable(path, "a.cs", "class A { }"), StringComparison.Ordinal);
        Assert.Equal("Готово.", _fixture.Chat.Messages[^1].Text);
    }

    // The summary reuses the model's request: same history and options plus a request to summarize (ADR 0023).
    [Fact]
    public async Task Summary_RepeatsTheConversationRequest_SoItIsReadFromTheCache()
    {
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, ContextWindow = 16_000, MaxOutputTokens = 1_000 });
        ScriptCalls(count: 5, size: 3_000).Reply("Резюме.").Reply("Готово.");

        await SendAsync();

        var requests = _fixture.Client.Requests;
        var (previous, summary) = (requests[^3], requests[^2]);
        Assert.Equal(HistoryTranscript.Render(previous), HistoryTranscript.Render(summary.Take(previous.Count)));
        Assert.Equal(CompactionPrompts.SummaryRequest, summary[^1].Text);
        var options = _fixture.Client.RequestOptions;
        Assert.Equal(options[^3]!.Instructions, options[^2]!.Instructions);
        Assert.Equal(options[^3]!.Tools!.Select(tool => tool.Name), options[^2]!.Tools!.Select(tool => tool.Name));
        Assert.Empty(_fixture.HelperClient.Requests);
    }

    // Retelling loses requirements: user requests stay verbatim, exact errors and code go to the full history file.
    [Fact]
    public async Task Summary_KeepsUserRequestsVerbatim_AndPointsToTheHistoryFile()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, ContextWindow = 16_000, MaxOutputTokens = 1_000 });
        ScriptCalls(count: 5, size: 3_000).Reply("Резюме.").Reply("Готово.");

        await SendAsync();

        var last = _fixture.Client.Requests[^1];
        var requests = last.Single(message => message.Contents.FirstOrDefault() is TextContent { Text: CompactedHistory.RequestsHeader });
        Assert.Equal("прочитай большие файлы", requests.Contents[1].ToString());
        var pointer = HistoryPointer().Match(Text(last));
        Assert.True(pointer.Success);
        var history = _fixture.FileSystem.ReadAllText(Path.Combine(AgentFixture.Root, pointer.Groups["path"].Value));
        Assert.Contains("результат 0\nxxxx", history, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"Full history before this compaction: (?<path>\S+) \(\d+ lines\)")]
    private static partial Regex HistoryPointer();

    private ScriptedChatClient ScriptCalls(int count, int size)
    {
        for (var n = 0; n < count; n++)
        {
            _fixture.Client.CallTool($"c{n}", "read_big", new Dictionary<string, object?> { ["n"] = n, ["size"] = size });
        }

        return _fixture.Client;
    }

    private Task SendAsync()
    {
        _fixture.Chat.Input = "прочитай большие файлы";
        return _fixture.Chat.SendCommand.ExecuteAsync(null);
    }

    private static string Text(IEnumerable<ChatMessage> request) =>
        string.Join('\n', request.SelectMany(message => message.Contents).Select(content => content switch
        {
            TextContent text => text.Text,
            FunctionResultContent result => result.Result?.ToString(),
            _ => null,
        }).OfType<string>());
}
