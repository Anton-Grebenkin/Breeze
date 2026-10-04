using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Documents.Resources;
using CodeEditor.Modules.Documents.Services.Changes;

namespace CodeEditor.Modules.Documents.Services;

/// <summary>
/// Agent feed lines for documents: "Read report.pdf · pages 1–5", "Created report.docx", "Changed budget.xlsx ·
/// 12 cells", "Combined total.pdf". Reading is exploration, so the feed collapses consecutive reads. A click opens the
/// document.
/// </summary>
public sealed class DocumentToolPresenter : IAgentToolPresenter
{
    private const int MaxQuote = 40;
    private const string Unknown = "?";

    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var path = call.Text("path");
        var name = ToolText.FileName(path ?? Unknown);
        return call.Name switch
        {
            DocumentAgentTools.ReadName => new AgentToolView(AgentToolIcon.Read, Format(call.IsDone ? Strings.FeedReadDone : Strings.FeedReadRunning, name))
            {
                IsExploration = true,
                FilePath = path,
                Detail = ReadDetail(call),
            },
            DocumentAgentTools.ChangeName => Change(call, name, path),
            _ => null,
        };
    }

    private static AgentToolView Change(AgentToolCall call, string name, string? path)
    {
        var action = call.Text("action") ?? Unknown;
        var (icon, running, done) = action switch
        {
            DocumentActions.CreateDocx or DocumentActions.CreatePdf or DocumentActions.CreateXlsx => (AgentToolIcon.Create, Strings.FeedCreateRunning, Strings.FeedCreateDone),
            DocumentActions.MergePdf or DocumentActions.ExtractPages => (AgentToolIcon.Create, Strings.FeedCombineRunning, Strings.FeedCombineDone),
            _ => (AgentToolIcon.Edit, Strings.FeedEditRunning, Strings.FeedEditDone),
        };
        return new AgentToolView(icon, Format(call.IsDone ? done : running, name)) { FilePath = path, Detail = ChangeDetail(call, action) };
    }

    private static string? ReadDetail(AgentToolCall call) =>
        call.Text("pages") is { } pages ? Format(Strings.FeedPages, pages)
        : call.Text("sheet") is { } sheet ? Format(Strings.FeedSheet, sheet) + (call.Text("range") is { } range ? " " + range : string.Empty)
        : call.Text("range");

    private static string? ChangeDetail(AgentToolCall call, string action) => action switch
    {
        DocumentActions.ReplaceText => Format(Strings.FeedReplace, Short(call.Text("find")), Short(call.Text("replace"))),
        DocumentActions.SetCells => Plural.Format(CellCount(call), Strings.CellForms),
        DocumentActions.AddSheet => Format(Strings.FeedSheet, call.Text("sheet") ?? Unknown),
        DocumentActions.RenameSheet => $"{call.Text("sheet") ?? Unknown} → {call.Text("newName") ?? Unknown}",
        DocumentActions.ExtractPages => Format(Strings.FeedPages, call.Text("pages") ?? Unknown),
        _ => null,
    };

    // Single cells plus the values in the rows block.
    private static int CellCount(AgentToolCall call) =>
        call.Items("cells").Count + call.Items("rows").Sum(row => row.ValueKind == JsonValueKind.Array ? row.GetArrayLength() : 0);

    private static string Short(string? text) => text is null ? string.Empty : text.Length <= MaxQuote ? text : text[..MaxQuote] + "…";

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
