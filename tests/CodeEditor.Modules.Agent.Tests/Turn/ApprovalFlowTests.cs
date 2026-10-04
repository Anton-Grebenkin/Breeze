using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Approvals;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

public sealed class ApprovalFlowTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private readonly EditTool _tool = new();

    public ApprovalFlowTests()
    {
        _fixture.ToolProviders.Add(_tool);
        _fixture.Previewers.Add(_tool);
    }

    private ChatViewModel Chat => _fixture.Chat;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Approve_RunsTool_ShowsDiff_AndContinues()
    {
        _fixture.Client.CallTool("c1", "apply_edits", Edit("a.cs")).Reply("Готово.");

        var sending = Send("переименуй");
        var card = await WaitForCardAsync();

        Assert.Equal("Агент предлагает правку", card.Title);
        var file = Assert.Single(card.Files);
        Assert.Equal(("Изменить a.cs", "+1 −1"), (file.Header, file.Counts));
        Assert.Equal(["−", "+"], file.Lines.Select(line => line.Marker));
        Assert.Empty(_tool.Applied);

        card.ApproveCommand.Execute(null);
        await sending;

        Assert.Equal(["a.cs"], _tool.Applied);
        Assert.Equal(ApprovalState.Approved, card.State);
        Assert.Equal("Готово.", Chat.Messages[^1].Text);
        Assert.False(Chat.IsBusy);
    }

    [Fact]
    public async Task AfterApproval_ModelStillSeesUserRequest()
    {
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create((string path) => "class A { }", "read_file"));
        _fixture.Client.CallTool("r1", "read_file", Edit("a.cs")).CallTool("c1", "apply_edits", Edit("a.cs")).Reply("Готово.").Reply("Второй ответ.");

        var sending = Send("переименуй");
        (await WaitForCardAsync()).ApproveCommand.Execute(null);
        await sending;
        await Send("ещё вопрос");

        Assert.All(_fixture.Client.Requests.Skip(1), request =>
            Assert.Contains(request, message => message.Role == ChatRole.User && message.Text.StartsWith("переименуй", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Reject_DoesNotRunTool_AndTellsModel()
    {
        _fixture.Client.CallTool("c1", "apply_edits", Edit("a.cs")).Reply("Понял, не меняю.");

        var sending = Send("переименуй");
        (await WaitForCardAsync()).RejectCommand.Execute(null);
        await sending;

        Assert.Empty(_tool.Applied);
        Assert.Equal("Понял, не меняю.", Chat.Messages[^1].Text);
        Assert.Contains(_fixture.Client.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            result => result.Result?.ToString()?.Contains("отклонил", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ApproveForChat_SkipsNextQuestion_UntilNewChat()
    {
        _fixture.Client
            .CallTool("c1", "apply_edits", Edit("a.cs")).Reply("Первая готова.")
            .CallTool("c2", "apply_edits", Edit("b.cs")).Reply("Вторая готова.");

        var first = Send("первая");
        (await WaitForCardAsync()).ApproveForChatCommand.Execute(null);
        await first;

        await Send("вторая");

        Assert.Equal(["a.cs", "b.cs"], _tool.Applied);
        var cards = Chat.Messages.Where(message => message.Kind == ChatMessageKind.Approval).Select(message => message.Approval!).ToList();
        Assert.Single(cards);
        Assert.All(cards, card => Assert.Equal(ApprovalState.Approved, card.State));
        Assert.Equal(
            ["Edit apply_edits (files: 1): applied and allowed for the chat", "Edit apply_edits (files: 1): applied without asking (allowed for the chat)"],
            _fixture.ActivityLog.Entries.Select(entry => entry.Message).Where(message => message.StartsWith("Edit ", StringComparison.Ordinal)));
    }

    // "Allow for this chat" doesn't skip calls a module always confirms (git push, deleting a container).
    [Fact]
    public async Task AlwaysAskedCall_AsksEvenAfterApproveForChat()
    {
        _fixture.Policies.Add(new FakePolicy { AlwaysAskedPath = "push.cs" });
        _fixture.Client
            .CallTool("c1", "apply_edits", Edit("a.cs")).Reply("Первая готова.")
            .CallTool("c2", "apply_edits", Edit("push.cs")).Reply("Вторая готова.");

        var first = Send("первая");
        (await WaitForCardAsync()).ApproveForChatCommand.Execute(null);
        await first;
        var second = Send("вторая");
        var card = await WaitForCardAsync();
        Assert.Equal(["a.cs"], _tool.Applied);
        card.ApproveCommand.Execute(null);
        await second;

        Assert.Equal(["a.cs", "push.cs"], _tool.Applied);
    }

    [Fact]
    public async Task Stop_WhileWaiting_RejectsCard()
    {
        _fixture.Client.CallTool("c1", "apply_edits", Edit("a.cs"));

        var sending = Send("переименуй");
        var card = await WaitForCardAsync();
        Chat.StopCommand.Execute(null);
        await sending;

        Assert.Equal(ApprovalState.Rejected, card.State);
        Assert.Empty(_tool.Applied);
        Assert.False(Chat.IsBusy);
    }

    [Fact]
    public async Task InvalidEdit_SkipsCard_AndModelGetsError()
    {
        _fixture.Client.CallTool("c1", "apply_edits", Edit("missing.cs")).Reply("Ошибся, сейчас исправлю.");

        await Send("переименуй");

        Assert.DoesNotContain(Chat.Messages, message => message.Kind == ChatMessageKind.Approval);
        var result = _fixture.Client.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single();
        Assert.Equal("Ошибка: missing.cs: нет файла", result.Result?.ToString());
    }

    // Plan mode keeps the edit tool (so the prefix doesn't change) but rejects edits without a card.
    [Fact]
    public async Task PlanMode_RejectsEdit_WithoutCard_AndTellsModelWhy()
    {
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, Mode = AgentMode.Plan });
        _fixture.Client.CallTool("c1", "apply_edits", Edit("a.cs")).Reply("План: поправить a.cs.");

        await Send("переименуй");

        Assert.Empty(_tool.Applied);
        Assert.DoesNotContain(Chat.Messages, message => message.Kind == ChatMessageKind.Approval);
        var result = _fixture.Client.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single();
        Assert.Contains("в режиме «План» изменения недоступны", result.Result?.ToString(), StringComparison.Ordinal);
    }

    // A call the owning module deems safe (a read-only command, a user rule) runs without a card.
    [Fact]
    public async Task PreapprovedByModulePolicy_RunsWithoutCard()
    {
        _fixture.Policies.Add(new FakePolicy { Preapproved = true });
        _fixture.Client.CallTool("c1", "apply_edits", Edit("a.cs")).Reply("Готово.");

        await Send("переименуй");

        Assert.Equal(["a.cs"], _tool.Applied);
        Assert.DoesNotContain(Chat.Messages, message => message.Kind == ChatMessageKind.Approval);
        Assert.Contains(_fixture.ActivityLog.Entries, entry => entry.Message.Contains("approved without asking", StringComparison.Ordinal));
    }

    // When the module suggests a rule, "Always allow" saves it and runs the call.
    [Fact]
    public async Task OfferedRule_AllowAlways_SavesRule_AndRuns()
    {
        var policy = new FakePolicy { Rule = "dotnet test" };
        _fixture.Policies.Add(policy);
        _fixture.Client.CallTool("c1", "apply_edits", Edit("a.cs")).Reply("Готово.");

        var sending = Send("переименуй");
        var card = await WaitForCardAsync();
        Assert.True(card.HasRule);
        Assert.Equal("Всегда разрешать «dotnet test»", card.AllowAlwaysText);
        card.AllowAlwaysCommand.Execute(null);
        await sending;

        Assert.Equal(["dotnet test"], policy.Allowed);
        Assert.Equal(["a.cs"], _tool.Applied);
        Assert.Equal(ApprovalState.Approved, card.State);
    }

    private static Dictionary<string, object?> Edit(string path) => new() { ["path"] = path };

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

    /// <summary>Test module policy: preapproves calls, or suggests a rule and records allowed ones.</summary>
    private sealed class FakePolicy : IAgentApprovalPolicy
    {
        public bool Preapproved { get; init; }

        public string? Rule { get; init; }

        /// <summary>Edits of this file always need a card.</summary>
        public string? AlwaysAskedPath { get; init; }

        public List<string> Allowed { get; } = [];

        public bool CanDecide(string toolName) => toolName == "apply_edits";

        public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments) => Preapproved;

        public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) => Preapproved;

        public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) => Rule;

        public void AllowAlways(string toolName, string rule) => Allowed.Add(rule);

        public bool AlwaysAsks(string toolName, IDictionary<string, object?> arguments) =>
            AlwaysAskedPath is not null && ToolArguments.Get<string>(arguments, "path") == AlwaysAskedPath;
    }

    /// <summary>Test edit tool: previews Run → Execute and records the path when applied.</summary>
    private sealed class EditTool : IAgentToolProvider, IAgentChangePreviewer
    {
        public List<string> Applied { get; } = [];

        public IEnumerable<AITool> CreateTools() =>
            [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(Apply, "apply_edits"))];

        public bool CanPreview(string toolName) => toolName == "apply_edits";

        public Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken)
        {
            var path = ToolArguments.Get<string>(arguments, "path");
            return path == "missing.cs"
                ? throw new AgentToolException("missing.cs: нет файла")
                : Task.FromResult<IReadOnlyList<FileChangePreview>>([new FileChangePreview(ProposedChangeKind.Edit, path, "void Run() { }", "void Execute() { }")]);
        }

        private string Apply(string path)
        {
            if (path == "missing.cs")
            {
                throw new AgentToolException("missing.cs: нет файла");
            }

            Applied.Add(path);
            return "applied " + path;
        }
    }
}
