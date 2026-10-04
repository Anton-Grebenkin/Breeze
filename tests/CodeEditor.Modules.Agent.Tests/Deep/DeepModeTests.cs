using System.Text;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Agent.Services.Deep;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Deep;

/// <summary>
/// Deep mode (ADR 0012) with the helper model as advisor and reviewer: the first edit goes to the advisor, a repeated
/// build failure brings advice, a reviewer checks the changes after green build and tests; tests are required; Agent
/// mode has no advisor.
/// </summary>
public sealed class DeepModeTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private readonly DeepTools _tools;
    private readonly StubVerifier _verifier = new();

    public DeepModeTests()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.FileSystem.AddFile(Path.Combine(AgentFixture.Root, "a.cs"), "class A { }\n");
        _tools = new DeepTools(_fixture);
        _fixture.ToolProviders.Add(_tools);
        _fixture.Verifiers.Add(_verifier);
        SetMode(AgentMode.Deep);
    }

    private ChatViewModel Chat => _fixture.Chat;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task FirstEdit_GoesToAdvisor_ThenIsMadeAgain()
    {
        _verifier.CanBuild = false;
        _fixture.HelperClient.Reply("Риск: Parse(null) не обработан. Проверка: тест на null.");
        _fixture.Client.CallTool("e1", "apply_edits", File("a.cs")).CallTool("e2", "apply_edits", File("a.cs")).Reply("Готово.");

        await Send("исправь разбор");

        Assert.Equal(["a.cs"], _tools.Applied);
        Assert.Contains("Пока не применено", ToolResults(1).Single(), StringComparison.Ordinal);
        Assert.Contains("Parse(null) не обработан", ToolResults(1).Single(), StringComparison.Ordinal);
        Assert.Contains(Chat.Messages, message => message.Kind == ChatMessageKind.Progress && message.Text.StartsWith("**Советник** · риски", StringComparison.Ordinal));
        Assert.Equal(AdvisorPrompts.BeforeFirstEdit, _fixture.HelperClient.LastOptions!.Instructions);
    }

    [Fact]
    public async Task SecondBuildFailureInRow_BringsAdvisorAdvice()
    {
        _tools.BuildResults.Enqueue(false);
        _tools.BuildResults.Enqueue(false);
        _fixture.HelperClient.Reply("Похоже, не хватает using System.Linq.");
        _fixture.Client.CallTool("b1", "build", NoArgs()).CallTool("b2", "build", NoArgs()).Reply("Не собирается.");

        await Send("собери");

        Assert.DoesNotContain("Советник", ToolResults(1).Single(), StringComparison.Ordinal);
        Assert.Contains("потому что сборка не прошла дважды подряд", ToolResults(2).Single(), StringComparison.Ordinal);
        Assert.Contains("не хватает using System.Linq", ToolResults(2).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GreenChecks_ThenReviewFindings_SendAgentBack_ThenPass()
    {
        _fixture.HelperClient
            .Reply("VERDICT: ISSUES\n[blocking] a.cs:1 — пустой класс; evidence: new A().Run() не компилируется")
            .Reply("VERDICT: PASS");
        _fixture.Client
            .CallTool("e1", "edit", File("a.cs")).CallTool("b1", "build", NoArgs()).CallTool("t1", "run_tests", NoArgs()).Reply("Готово.")
            .CallTool("e2", "edit", File("a.cs")).CallTool("b2", "build", NoArgs()).CallTool("t2", "run_tests", NoArgs()).Reply("Исправил замечание.");

        await Send("добавь метод Run");

        var statuses = Chat.Messages.Where(message => message.Kind == ChatMessageKind.Status).Select(message => message.Text).ToList();
        Assert.Contains("Независимая проверка: блокирующих замечаний — 1, агент разбирает", statuses);
        Assert.Contains("Независимая проверка: доказанных ошибок нет", statuses);
        var review = _fixture.Client.Requests.Select(request => request[^1]).Single(message => message.Text.StartsWith("<reminder>Независимый ревьюер", StringComparison.Ordinal));
        Assert.Contains("[blocking] a.cs:1 — пустой класс", review.Text, StringComparison.Ordinal);
        Assert.Contains(Chat.Messages, message => message.Kind == ChatMessageKind.Progress && message.Text.StartsWith("**Ревьюер**", StringComparison.Ordinal));
        Assert.Equal("Исправил замечание.", Chat.Messages.Last(message => message.Kind == ChatMessageKind.Assistant).Text);
    }

    [Fact]
    public async Task CodeChanges_NeedTestsToo()
    {
        _fixture.Client.CallTool("e1", "edit", File("a.cs")).CallTool("b1", "build", NoArgs()).Reply("Готово.").CallTool("t1", "run_tests", NoArgs()).Reply("Тесты прошли.");
        _fixture.HelperClient.Reply("VERDICT: PASS");

        await Send("поправь");

        Assert.Contains("run_tests", _fixture.Client.Requests[3][^1].Text, StringComparison.Ordinal);
        Assert.Equal("Тесты прошли.", Chat.Messages.Last(message => message.Kind == ChatMessageKind.Assistant).Text);
    }

    [Fact]
    public async Task AgentMode_HasNoAdvisorOrReview()
    {
        SetMode(AgentMode.Agent);
        _fixture.Client.CallTool("e1", "apply_edits", File("a.cs")).CallTool("b1", "build", NoArgs()).Reply("Готово.");

        await Send("поправь");

        Assert.Empty(_fixture.HelperClient.Requests);
        Assert.Equal(["a.cs"], _tools.Applied);
    }

    [Fact]
    public void Digest_SkipsServiceBlocks_AndShortensToolResults()
    {
        var digest = TranscriptDigest.Render(
        [
            new ChatMessage(ChatRole.User, [new TextContent("почини тест"), new TextContent("<context>\nDate: 2026-09-29.\n</context>"), new TextContent("<reminder>Mode: Deep</reminder>")]),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c1", "read_file", new Dictionary<string, object?> { ["path"] = "a.cs" })]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("c1", new string('x', 5_000))]),
        ]);

        Assert.StartsWith("USER: почини тест", digest, StringComparison.Ordinal);
        Assert.DoesNotContain("<context>", digest, StringComparison.Ordinal);
        Assert.Contains("CALL read_file", digest, StringComparison.Ordinal);
        Assert.True(digest.Length < 2_000);
    }

    [Fact]
    public void ReviewResult_OnlyMarkedLinesAreFindings()
    {
        var result = ReviewResult.Parse("VERDICT: ISSUES\n- [blocking] a.cs:3 — ошибка; evidence: x\n[advisory] b.cs:1 — стоит проверить\nпросто текст");

        Assert.Equal(["a.cs:3 — ошибка; evidence: x"], result.Blocking);
        Assert.Equal(["b.cs:1 — стоит проверить"], result.Advisory);
        Assert.True(ReviewResult.Parse("VERDICT: PASS").Passed);
    }

    private void SetMode(AgentMode mode) => _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, Mode = mode, Approvals = AgentApprovals.Auto, AutoMemory = false });

    private static Dictionary<string, object?> File(string path) => new() { ["path"] = path };

    private static Dictionary<string, object?> NoArgs() => [];

    private List<string> ToolResults(int request) =>
        [.. _fixture.Client.Requests[request][^1].Contents.OfType<FunctionResultContent>().Select(result => result.Result?.ToString() ?? string.Empty)];

    private Task Send(string text)
    {
        Chat.Input = text;
        return Chat.SendCommand.ExecuteAsync(null);
    }

    private sealed class StubVerifier : IAgentVerifier
    {
        public bool CanBuild { get; set; } = true;

        public bool CanTest { get; set; } = true;
    }

    /// <summary>An approved edit, an unapproved edit (for review), and build and tests reporting to the gate.</summary>
    private sealed class DeepTools(AgentFixture fixture) : IAgentToolProvider
    {
        public List<string> Applied { get; } = [];

        public Queue<bool> BuildResults { get; } = new();

        public IEnumerable<AITool> CreateTools() =>
        [
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string path) =>
            {
                Applied.Add(path);
                return "applied " + path;
            }, "apply_edits")),
            AIFunctionFactory.Create(Edit, "edit"),
            AIFunctionFactory.Create(Build, "build"),
            AIFunctionFactory.Create(() => "Тесты прошли." + fixture.Gate.Record(VerificationKind.Tests, succeeded: true), "run_tests"),
        ];

        private string Edit(string path)
        {
            var full = Path.Combine(AgentFixture.Root, path);
            var old = fixture.FileSystem.ReadAllText(full);
            var text = old.Replace("{ }", "{ void Run() { } }", StringComparison.Ordinal);
            fixture.FileSystem.WriteAllBytesAtomic(full, Encoding.UTF8.GetBytes(text));
            fixture.FileState.RecordWrite(full, text, old);
            return "ok";
        }

        private string Build()
        {
            var succeeded = !BuildResults.TryDequeue(out var result) || result;
            return (succeeded ? "Сборка успешна." : "Ошибки сборки.") + fixture.Gate.Record(VerificationKind.Build, succeeded);
        }
    }
}
