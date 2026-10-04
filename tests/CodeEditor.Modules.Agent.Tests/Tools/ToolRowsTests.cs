using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Tools;

/// <summary>Tool rows in the feed (ADR 0010): module descriptions, the fallback row, failures, saved history.</summary>
public sealed class ToolRowsTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public ToolRowsTests()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.ToolProviders.Add(new ProbeTools());
        _fixture.Presenters.Add(new ProbePresenter());
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Row_ShowsDoneTitleAndDetail_AfterResult()
    {
        _fixture.Client.CallTool("c1", "probe", new Dictionary<string, object?> { ["path"] = "src/a.cs" }).Reply("Готово.");

        await _fixture.SendAsync("проверь");

        var row = Assert.Single(_fixture.Chat.Messages, message => message.Kind == ChatMessageKind.Tool);
        Assert.Equal(("Проверен a.cs", "итог: ok", "src/a.cs", false), (row.Text, row.Tool!.Detail, row.Tool.FilePath, row.Tool.IsFailure));
        Assert.False(row.IsInProgress);
    }

    [Fact]
    public async Task Row_OfUnknownTool_FallsBackToNameAndArguments()
    {
        _fixture.Client.CallTool("c1", "unknown", new Dictionary<string, object?> { ["x"] = "1" }).Reply("Готово.");

        await _fixture.SendAsync("проверь");

        var row = Assert.Single(_fixture.Chat.Messages, message => message.Kind == ChatMessageKind.Tool);
        Assert.Equal(("unknown(x: 1)", AgentToolIcon.Other), (row.Text, row.Tool!.Icon));
    }

    [Fact]
    public void ErrorResult_MarksFailure_EvenIfPresenterDoesNot()
    {
        var views = new ToolViews([new ProbePresenter()], NullLogger<ToolViews>.Instance);
        var call = new FunctionCallContent("c1", "probe", new Dictionary<string, object?> { ["path"] = "a.cs" });

        var view = views.Describe(call, new FunctionResultContent("c1", ToolViews.ErrorPrefix + " файла нет"));

        Assert.True(view.IsFailure);
    }

    [Fact]
    public async Task SavedChat_RestoresToolRowIconAndDetail()
    {
        _fixture.Client.CallTool("c1", "probe", new Dictionary<string, object?> { ["path"] = "src/a.cs" }).Reply("Готово.");
        await _fixture.SendAsync("проверь");

        var restored = _fixture.CreateChat(_fixture.CreateHistory());
        restored.Session.LoadLatest();

        var row = Assert.Single(restored.Messages, message => message.Kind == ChatMessageKind.Tool);
        Assert.Equal(("Проверен a.cs", AgentToolIcon.Read, "итог: ok"), (row.Text, row.Tool!.Icon, row.Tool.Detail));
    }

    private sealed class ProbeTools : IAgentToolProvider
    {
        public IEnumerable<AITool> CreateTools() =>
        [
            AIFunctionFactory.Create((string path) => "ok", "probe"),
            AIFunctionFactory.Create((string x) => "ok", "unknown"),
        ];
    }

    private sealed class ProbePresenter : IAgentToolPresenter
    {
        public AgentToolView? Present(AgentToolCall call) => call.Name != "probe"
            ? null
            : new AgentToolView(AgentToolIcon.Read, call.IsDone ? $"Проверен {ToolText.FileName(call.Text("path")!)}" : "Проверка")
            {
                FilePath = call.Text("path"),
                Detail = call.Result is { } result ? $"итог: {result}" : null,
                IsExploration = true,
            };
    }
}
