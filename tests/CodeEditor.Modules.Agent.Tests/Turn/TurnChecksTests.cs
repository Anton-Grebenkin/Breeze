using System.Text;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Agent.Services.Deep;
using CodeEditor.Modules.Agent.Services.Prompts;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>
/// Checks before a turn ends: the build gate in Agent mode, correction after failures, tools in read-only modes.
/// </summary>
public sealed class TurnChecksTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private readonly CheckTools _tools;
    private readonly StubVerifier _verifier = new();

    public TurnChecksTests()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.FileSystem.AddFile(Path.Combine(AgentFixture.Root, "a.cs"), "class A { }\n").AddFile(Path.Combine(AgentFixture.Root, "README.md"), "# A\n");
        _tools = new CheckTools(_fixture);
        _fixture.ToolProviders.Add(_tools);
        _fixture.Verifiers.Add(_verifier);
        SetMode(AgentMode.Agent);
    }

    private ChatViewModel Chat => _fixture.Chat;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Agent_EditWithoutBuild_Reminds_ThenBuildEndsTurn()
    {
        _fixture.Client.CallTool("c1", "edit", File("a.cs")).Reply("Готово.").CallTool("c2", "build", NoArgs()).Reply("Собрал, всё чисто.");

        await Send("исправь");

        Assert.Equal(4, _fixture.Client.Requests.Count);
        Assert.Contains("не запускал build", LastUserText(2), StringComparison.Ordinal);
        Assert.Contains(Chat.Messages, message => message is { Kind: ChatMessageKind.Status, Text: "Проверка: после правок не было сборки — агент проверяет" });
        Assert.Equal("Собрал, всё чисто.", Chat.Messages[^1].Text);
    }

    [Fact]
    public async Task Agent_BuildKeepsFailing_TwoReminders_ThenUnverified_WithCorrectionHint()
    {
        _tools.BuildResults.Enqueue(false);
        _tools.BuildResults.Enqueue(false);
        _fixture.Client.CallTool("c1", "edit", File("a.cs")).Reply("Готово.")
            .CallTool("c2", "build", NoArgs()).Reply("Не собирается.")
            .CallTool("c3", "build", NoArgs()).Reply("Всё ещё не собирается.");

        await Send("исправь");

        Assert.Equal(6, _fixture.Client.Requests.Count);
        Assert.Contains("Сборка после твоих правок не прошла", LastUserText(4), StringComparison.Ordinal);
        Assert.Contains(ToolResults(5), result => result.Contains("Сборка не проходит второй раз подряд", StringComparison.Ordinal));
        Assert.Equal("Правки не проверены: сборка после них не прошла.", Chat.Messages[^1].Text);
    }

    [Fact]
    public async Task NoGate_ForNonCodeEdits()
    {
        _fixture.Client.CallTool("c1", "edit", File("README.md")).Reply("Готово.");

        await Send("исправь");

        Assert.Equal(2, _fixture.Client.Requests.Count);
        Assert.DoesNotContain(Chat.Messages, message => message.Kind == ChatMessageKind.Status);
    }

    // The tool list is the same in every mode (the prefix is cached); the mode refuses changes at call time (ADR 0012).
    [Theory]
    [InlineData(AgentMode.Ask)]
    [InlineData(AgentMode.Plan)]
    public async Task ReadOnlyModes_KeepToolList_ButRefuseChanges(AgentMode mode)
    {
        SetMode(mode);
        _fixture.ToolProviders.Add(new NamedTools(WorkflowAgentTools.AskUserName));
        _fixture.Client.CallTools(("c1", "build", NoArgs()), ("c2", "edit", File("a.cs"))).Reply("Ответ.");

        await Send("как устроено?");

        var tools = _fixture.Client.LastOptions!.Tools!.Select(tool => tool.Name).ToList();
        Assert.Contains("edit", tools);
        Assert.Contains("build", tools);
        Assert.Equal([GuardedToolFunction.Refusal("build", mode), GuardedToolFunction.Refusal("edit", mode)], ToolResults(1));
        Assert.Equal("class A { }\n", _fixture.FileSystem.ReadAllText(Path.Combine(AgentFixture.Root, "a.cs")));
        Assert.Equal("Ответ.", Chat.Messages.Last(message => message.Kind == ChatMessageKind.Assistant).Text);
    }

    [Fact]
    public async Task NoGate_WhenNothingToBuild()
    {
        _verifier.CanBuild = false;
        _fixture.Client.CallTool("c1", "edit", File("a.cs")).Reply("Готово.");

        await Send("исправь");

        Assert.Equal(2, _fixture.Client.Requests.Count);
    }

    // A report while plan items are open gets one reminder per turn; a question to the user is a legitimate stop.
    [Fact]
    public async Task OpenPlanItems_RemindOnce_ButNotAfterAQuestion()
    {
        _fixture.Todos.Replace([new TodoItem("1", "Прочитать код", TodoStatus.Completed), new TodoItem("2", "Поправить разбор", TodoStatus.InProgress)]);
        _fixture.Client.Reply("Промежуточный отчёт.").Reply("Всё ещё не всё.").Reply("Какой вариант выбрать?");

        await Send("сделай по плану");
        await Send("дальше");

        var reminder = LastUserText(1);
        Assert.Contains("В плане остались открытые пункты", reminder, StringComparison.Ordinal);
        Assert.Contains("[>] 2. Поправить разбор", reminder, StringComparison.Ordinal);
        Assert.DoesNotContain("[x] 1.", reminder, StringComparison.Ordinal);
        Assert.Equal(3, _fixture.Client.Requests.Count);
        Assert.Equal("Какой вариант выбрать?", Chat.Messages[^1].Text);
    }

    // A request with several requirements gets one item-by-item audit after edits and a build (ADR 0016).
    [Fact]
    public async Task RequirementsAudit_AfterCodeEdits_OnMultiRequirementRequest_Once()
    {
        _fixture.Client.CallTool("c1", "edit", File("a.cs")).CallTool("c2", "build", NoArgs()).Reply("Готово.").Reply("Сверил: всё на месте.");

        await Send("Сделай:\n- правило 1\n- правило 2\n- правило 3");

        Assert.Equal(4, _fixture.Client.Requests.Count);
        Assert.Contains("сверь результат с запросом", LastUserText(3), StringComparison.Ordinal);
        Assert.Contains(Chat.Messages, message => message is { Kind: ChatMessageKind.Status, Text: "Сверка с запросом — агент проверяет каждый пункт" });
        Assert.Equal("Сверил: всё на месте.", Chat.Messages[^1].Text);
    }

    // The audit request is in the turn reminder up front; a report citing code locations skips the fallback audit.
    [Fact]
    public async Task RequirementsReminder_UpFront_AndReportWithEvidence_EndsTurn()
    {
        _fixture.Client.CallTool("c1", "edit", File("a.cs")).CallTool("c2", "build", NoArgs()).Reply("Сделано: правило 1 — a.cs:1, правило 2 — a.cs:2.");

        await Send("Сделай:\n- правило 1\n- правило 2\n- правило 3");

        Assert.Contains(PromptSections.RequirementsReminder, _fixture.Client.Requests[0][^1].Contents.OfType<TextContent>().Last().Text, StringComparison.Ordinal);
        Assert.Equal(3, _fixture.Client.Requests.Count);
    }

    // A harness prompt is an editor note, not the user's words; the earlier answer becomes progress, leaving one answer.
    [Fact]
    public async Task CheckPrompt_IsEditorNote_AndEarlierAnswerBecomesProgress()
    {
        _fixture.Client.CallTool("c1", "edit", File("a.cs")).CallTool("c2", "build", NoArgs()).Reply("Готово.").Reply("Сверил: всё на месте.");

        await Send("Сделай:\n- правило 1\n- правило 2\n- правило 3");

        Assert.StartsWith("<reminder>", LastUserText(3), StringComparison.Ordinal);
        Assert.Contains("not the user", LastUserText(3), StringComparison.Ordinal);
        Assert.Equal("Сверил: всё на месте.", Assert.Single(Chat.Messages, message => message.Kind == ChatMessageKind.Assistant).Text);
        Assert.Contains(Chat.Messages, message => message is { Kind: ChatMessageKind.Progress, Text: "Готово." });
        Assert.DoesNotContain("сверь результат", TranscriptDigest.Render(_fixture.Client.Requests[^1]), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("исправь", true)]
    [InlineData("Сделай:\n- правило 1\n- правило 2\n- правило 3", false)]
    public async Task RequirementsAudit_NotForShortRequest_OrWithoutCodeEdits(string request, bool edits)
    {
        if (edits)
        {
            _fixture.Client.CallTool("c1", "edit", File("a.cs")).CallTool("c2", "build", NoArgs());
        }

        _fixture.Client.Reply("Готово.");

        await Send(request);

        Assert.Equal(edits ? 3 : 1, _fixture.Client.Requests.Count);
    }

    // Simultaneous checks go as one note: "build" and "audit" in one round, not two.
    [Fact]
    public async Task SimultaneousChecks_GoAsOneNote()
    {
        _fixture.Client.CallTool("c1", "edit", File("a.cs")).Reply("Готово.").CallTool("c2", "build", NoArgs()).Reply("Сделано: правило 1 — a.cs:1, правило 2 — a.cs:2.");

        await Send("Сделай:\n- правило 1\n- правило 2\n- правило 3");

        Assert.Equal(4, _fixture.Client.Requests.Count);
        var note = LastUserText(2);
        Assert.Contains("1. ", note, StringComparison.Ordinal);
        Assert.Contains("не запускал build", note, StringComparison.Ordinal);
        Assert.Contains("сверь результат с запросом", note, StringComparison.Ordinal);
        Assert.Contains(Chat.Messages, message => message.Kind == ChatMessageKind.Status && message.Text.Contains(" · ", StringComparison.Ordinal));
    }

    // A tool call written as text isn't an answer; in any mode the model is asked to make a real call (seen from Qwen).
    [Fact]
    public async Task ToolCallWrittenAsText_IsNotTheAnswer()
    {
        SetMode(AgentMode.Ask);
        _fixture.Client.Reply("<function=search_text> <parameter=query> X </parameter> </function>").Reply("Нашёл: a.cs:1.");

        await Send("где X?");

        Assert.Equal(2, _fixture.Client.Requests.Count);
        Assert.Contains("написан текстом", LastUserText(1), StringComparison.Ordinal);
        Assert.Equal("Нашёл: a.cs:1.", Assert.Single(Chat.Messages, message => message.Kind == ChatMessageKind.Assistant).Text);
    }

    // An empty answer (e.g. after a tool error) gets one reminder per turn instead of a silent end.
    [Fact]
    public async Task EmptyAnswer_RemindOnce()
    {
        _fixture.Client.Reply(string.Empty).Reply(" ").Reply("Готово.");

        await Send("исправь");
        await Send("дальше");

        Assert.Contains("последний ответ пустой", LastUserText(1), StringComparison.Ordinal);
        Assert.Contains(Chat.Messages, message => message is { Kind: ChatMessageKind.Status, Text: "Пустой ответ — агент продолжает" });
        Assert.Equal(3, _fixture.Client.Requests.Count);
    }

    [Fact]
    public void CorrectionHints_GrowWithFailuresInRow()
    {
        Assert.Null(CorrectionHints.AfterFailures(VerificationKind.Build, 1));
        Assert.Contains("второй раз подряд", CorrectionHints.AfterFailures(VerificationKind.Build, 2), StringComparison.Ordinal);
        Assert.Contains("Смени подход", CorrectionHints.AfterFailures(VerificationKind.Tests, 3), StringComparison.Ordinal);
    }

    // Edits are auto-approved: these tests check the gate, not the cards.
    private void SetMode(AgentMode mode) => _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, Mode = mode, Approvals = AgentApprovals.Auto });

    private static Dictionary<string, object?> File(string path) => new() { ["path"] = path };

    private static Dictionary<string, object?> NoArgs() => [];

    private string LastUserText(int request) => _fixture.Client.Requests[request].Last(message => message.Role == ChatRole.User).Text;

    private IEnumerable<string> ToolResults(int request) =>
        _fixture.Client.Requests[request].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Select(result => result.Result?.ToString() ?? string.Empty);

    private Task Send(string text)
    {
        Chat.Input = text;
        return Chat.SendCommand.ExecuteAsync(null);
    }

    /// <summary>Dummy tools with the given names; the mode decides which ones the model gets.</summary>
    private sealed class NamedTools(params string[] names) : IAgentToolProvider
    {
        public IEnumerable<AITool> CreateTools() => names.Select(name => AIFunctionFactory.Create(() => "ok", name));
    }

    private sealed class StubVerifier : IAgentVerifier
    {
        public bool CanBuild { get; set; } = true;

        public bool CanTest { get; set; } = true;
    }

    /// <summary>An unapproved edit plus checks that report to the gate, like the build module.</summary>
    private sealed class CheckTools(AgentFixture fixture) : IAgentToolProvider
    {
        public Queue<bool> BuildResults { get; } = new();

        public IEnumerable<AITool> CreateTools() =>
        [
            AIFunctionFactory.Create(Edit, "edit"),
            AIFunctionFactory.Create(Build, "build"),
            AIFunctionFactory.Create(RunTests, "run_tests"),
        ];

        private string Edit(string path)
        {
            var full = Path.Combine(AgentFixture.Root, path);
            var old = fixture.FileSystem.ReadAllText(full);
            fixture.FileSystem.WriteAllBytesAtomic(full, Encoding.UTF8.GetBytes("sealed " + old));
            fixture.FileState.RecordWrite(full, "sealed " + old, old);
            return "ok";
        }

        private string Build()
        {
            var succeeded = !BuildResults.TryDequeue(out var result) || result;
            return (succeeded ? "Сборка успешна." : "Ошибки сборки.") + fixture.Gate.Record(VerificationKind.Build, succeeded);
        }

        private string RunTests() => "Тесты прошли." + fixture.Gate.Record(VerificationKind.Tests, succeeded: true);
    }
}
