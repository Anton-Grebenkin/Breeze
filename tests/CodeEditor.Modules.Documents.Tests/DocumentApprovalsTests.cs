using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Documents.Services;
using CodeEditor.Modules.Documents.Services.Changes;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// Document change approvals: "Always allow" is per action, replacing an existing file always asks; feed rows for reads
/// and changes.
/// </summary>
public sealed class DocumentApprovalsTests
{
    private readonly TestOptionsMonitor<DocumentOptions> _options = new(new DocumentOptions { AlwaysAllow = [DocumentActions.SetCells] });
    private readonly FakeSettingsService _settings = new();
    private readonly DocumentApprovals _approvals;

    public DocumentApprovalsTests() => _approvals = new DocumentApprovals(_options, _settings, NullLogger<ActionApprovalPolicy>.Instance);

    [Fact]
    public void AllowedAction_RunsWithoutCard_OthersAsk()
    {
        Assert.True(_approvals.IsPreapproved(DocumentAgentTools.ChangeName, Call(DocumentActions.SetCells)));
        Assert.False(_approvals.IsPreapproved(DocumentAgentTools.ChangeName, Call(DocumentActions.ReplaceText)));
        Assert.False(_approvals.CanDecide(DocumentAgentTools.ReadName));
    }

    // The old document can't be recovered, so replacing a whole file always asks, even for an allowed action.
    [Fact]
    public void Overwrite_AlwaysAsks()
    {
        _options.Set(new DocumentOptions { AlwaysAllow = [DocumentActions.CreateDocx] });
        var overwrite = Call(DocumentActions.CreateDocx);
        overwrite["overwrite"] = true;

        Assert.False(_approvals.IsPreapproved(DocumentAgentTools.ChangeName, overwrite));
        Assert.Null(_approvals.SuggestRule(DocumentAgentTools.ChangeName, overwrite));
        Assert.True(_approvals.AlwaysAsks(DocumentAgentTools.ChangeName, overwrite));
        Assert.True(_approvals.IsPreapproved(DocumentAgentTools.ChangeName, Call(DocumentActions.CreateDocx)));
        Assert.False(_approvals.AlwaysAsks(DocumentAgentTools.ChangeName, Call(DocumentActions.CreateDocx)));
    }

    [Fact]
    public void AllowAlways_AddsTheActionToSettings()
    {
        Assert.Equal(DocumentActions.Insert, _approvals.SuggestRule(DocumentAgentTools.ChangeName, Call(DocumentActions.Insert)));

        _approvals.AllowAlways(DocumentAgentTools.ChangeName, DocumentActions.Insert);

        Assert.Equal(new[] { DocumentActions.SetCells, DocumentActions.Insert }, _settings.Written[DocumentApprovals.AlwaysAllowKey]);
    }

    [Fact]
    public void Feed_ReadIsExploration_AndOpensTheFile()
    {
        var presenter = new DocumentToolPresenter();

        var running = presenter.Present(new AgentToolCall(DocumentAgentTools.ReadName, new Dictionary<string, object?> { ["path"] = "docs/Отчёт.pdf", ["pages"] = "1-5" }))!;
        var done = presenter.Present(new AgentToolCall(DocumentAgentTools.ReadName, new Dictionary<string, object?> { ["path"] = "docs/Отчёт.pdf" }, "текст"))!;

        Assert.Equal(("Чтение Отчёт.pdf", "стр. 1-5", "docs/Отчёт.pdf", true), (running.Title, running.Detail, running.FilePath, running.IsExploration));
        Assert.Equal("Прочитан Отчёт.pdf", done.Title);
    }

    [Fact]
    public void Feed_ChangesSayWhatHappens()
    {
        var presenter = new DocumentToolPresenter();

        var created = presenter.Present(Change(DocumentActions.CreateXlsx, "budget.xlsx", "ok"))!;
        var cells = presenter.Present(Change(DocumentActions.SetCells, "budget.xlsx", null, ("cells", new[] { new { cell = "A1", value = "1" }, new { cell = "B1", value = "2" } }), ("rows", new[] { new object[] { 1, 2, 3 } })))!;
        var replaced = presenter.Present(Change(DocumentActions.ReplaceText, "a.docx", null, ("find", "старое"), ("replace", "новое")))!;
        var merged = presenter.Present(Change(DocumentActions.MergePdf, "all.pdf", "ok"))!;

        Assert.Equal((AgentToolIcon.Create, "Записан budget.xlsx"), (created.Icon, created.Title));
        Assert.Equal((AgentToolIcon.Edit, "Правка budget.xlsx", "5 ячеек"), (cells.Icon, cells.Title, cells.Detail));
        Assert.Equal("«старое» → «новое»", replaced.Detail);
        Assert.Equal("Страницы собраны в all.pdf", merged.Title);
        Assert.Null(presenter.Present(new AgentToolCall("read_file", new Dictionary<string, object?>())));
    }

    private static Dictionary<string, object?> Call(string action) => new() { ["action"] = action, ["path"] = "a.docx" };

    private static AgentToolCall Change(string action, string path, string? result, params (string Name, object? Value)[] more)
    {
        var arguments = new Dictionary<string, object?> { ["action"] = action, ["path"] = path };
        foreach (var (name, value) in more)
        {
            arguments[name] = value;
        }

        return new AgentToolCall(DocumentAgentTools.ChangeName, arguments, result);
    }
}
