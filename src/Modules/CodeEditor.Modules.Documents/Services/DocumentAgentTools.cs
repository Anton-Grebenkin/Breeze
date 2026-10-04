using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using CodeEditor.Modules.Documents.Services.Changes;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Documents.Services;

/// <summary>
/// Agent tools for documents (ADR 0034). <c>document</c> reads PDF, Word, PowerPoint, Excel and CSV without asking in
/// every mode (<see cref="ReadOnlyAIFunction"/>). <c>document_change</c> creates and edits documents, each call through a
/// card showing what will change (<see cref="IAgentChangePreviewer"/>); only what the user saw gets written
/// (<see cref="PreviewLedger"/>), and a fully replaced file goes to the Recycle Bin. Paths stay inside the workspace. A
/// changed document opens in a preview tab so the user sees the result right away.
/// </summary>
public sealed class DocumentAgentTools(
    DocumentFiles files, DocumentReading reading, DocumentChanges changes, IAgentOutputStore outputs, ICommandService commands, IUiDispatcher dispatcher)
    : IAgentToolProvider, IAgentChangePreviewer
{
    public const string ReadName = "document";
    public const string ChangeName = "document_change";

    private readonly PreviewLedger _previews = new();

    public IEnumerable<AITool> CreateTools() =>
    [
        new ReadOnlyAIFunction(AIFunctionFactory.Create(ReadAsync, ReadName,
            "Reads a document that read_file cannot: .pdf (text by pages), .docx (Markdown: headings, lists, tables, **bold**, *italic*), " +
            ".pptx (slides with titles, text, tables and speaker notes), .xlsx (the sheet list and a sheet as a table with column letters and " +
            "row numbers; formulas as '=SUM(B2:B5) → 42') and .csv. Long output is saved to a file: you get its start and end and the path.")),
        new ApprovalRequiredAIFunction(new TextValuesFunction(AIFunctionFactory.Create(ChangeAsync, ChangeName,
            "Creates or changes a document; the user sees and approves each call. Actions: create_docx (Word from Markdown), " +
            "create_pdf (PDF from Markdown), create_xlsx (Excel from sheets of rows: bold header, column widths by content), replace_text (Word: " +
            "replace text, keeping the formatting), insert (Word: add Markdown after the paragraph containing 'after', or at the end), set_cells " +
            "(Excel: write cells and/or rows from start), add_sheet, rename_sheet (references in formulas follow), merge_pdf (join PDFs or their " +
            "pages into a new file), extract_pages (copy pages of a PDF into a new file). Cell values are typed as in Excel: 12.5 is a number, " +
            "=SUM(B2:B5) a formula, TRUE a boolean, '00123 text, an empty value clears the cell; formulas use English function names and " +
            "commas: =IF(B2>0,\"yes\",\"no\"). Read the document first and copy text for find and after exactly. create_* fail if the file " +
            "exists unless overwrite is true; the replaced file goes to the Recycle Bin."))),
    ];

    public bool CanPreview(string toolName) => toolName == ChangeName;

    public async Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var change = DocumentArguments.Change(arguments);
        try
        {
            var plan = await Task.Run(() => changes.Plan(change), cancellationToken);
            _previews.Shown(change, files.Fingerprint(plan.Target));
            return [plan.Preview];
        }
        catch (AgentToolException)
        {
            _previews.Failed(change);
            throw;
        }
    }

    private async Task<string> ReadAsync(
        [Description("The document relative to the workspace root: .pdf, .docx, .pptx, .xlsx or .csv.")] string path,
        [Description("PDF: pages, PPTX: slides to read — '3', '2-5' or '1,4-6'; default all.")] string? pages = null,
        [Description("XLSX: the sheet to read; default the first sheet.")] string? sheet = null,
        [Description("XLSX, CSV: the cells to read — 'A1:F50', 'B:D' or '10:40'; default the used range, up to 500 rows.")] string? range = null,
        CancellationToken cancellationToken = default)
    {
        var document = files.Resolve(path, forWriting: false);
        var text = await Task.Run(() => reading.Read(document, DocumentArguments.Blank(pages), DocumentArguments.Blank(sheet), DocumentArguments.Blank(range), cancellationToken), cancellationToken);
        return outputs.Fit(text, ReadName);
    }

    private async Task<string> ChangeAsync(
        [Description("create_docx, create_pdf, create_xlsx, replace_text, insert, set_cells, add_sheet, rename_sheet, merge_pdf or extract_pages.")] string action,
        [Description("The document to create or change, relative to the workspace root.")] string path,
        [Description("create_docx, create_pdf, insert: the content in Markdown — # headings, paragraphs, **bold**, *italic*, - and 1. lists, | tables |.")] string? markdown = null,
        [Description("create_xlsx: the sheets of the new workbook.")] SheetInput[]? sheets = null,
        [Description("replace_text: the text to find; it must occur once unless all is true.")] string? find = null,
        [Description("replace_text: the new text.")] string? replace = null,
        [Description("replace_text: replace every occurrence.")] bool all = false,
        [Description("insert: text of the paragraph or table to insert after; omit to append at the end.")] string? after = null,
        [Description("set_cells, add_sheet, rename_sheet: the sheet; set_cells defaults to the first sheet.")] string? sheet = null,
        [Description("set_cells: single cells to write, e.g. [{\"cell\": \"B2\", \"value\": \"10\"}, {\"cell\": \"C2\", \"value\": \"=B2*2\"}].")] CellInput[]? cells = null,
        [Description("set_cells, add_sheet: rows of values written from start (add_sheet: from A1).")] JsonElement[][]? rows = null,
        [Description("set_cells: the top-left cell for rows; default A1.")] string? start = null,
        [Description("rename_sheet: the new sheet name.")] string? newName = null,
        [Description("merge_pdf: the PDFs to join in order, each with optional pages.")] PdfSourceInput[]? sources = null,
        [Description("extract_pages: the source PDF.")] string? source = null,
        [Description("extract_pages: the pages to copy, e.g. '1-3,7'.")] string? pages = null,
        [Description("create_docx, create_pdf, create_xlsx, merge_pdf, extract_pages: replace the file if it exists.")] bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        var change = new DocumentChange(action, path)
        {
            Markdown = markdown, Sheets = sheets ?? [], Find = find, Replace = replace, All = all, After = DocumentArguments.Blank(after), Sheet = DocumentArguments.Blank(sheet),
            Cells = cells ?? [], Rows = rows ?? [], Start = DocumentArguments.Blank(start), NewName = newName, Sources = sources ?? [], Source = source, Pages = pages,
            Overwrite = overwrite,
        };
        var plan = await Task.Run(() => changes.Plan(change), cancellationToken);
        EnsureSeen(change, plan);
        var replaced = change.Overwrite && DocumentActions.Creates(change.Action) && files.Exists(plan.Target);
        files.Write(plan.Target, plan.Bytes, recycleExisting: replaced);
        await dispatcher.InvokeAsync(() => _ = commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(plan.Target.Full, Preview: true), CancellationToken.None));
        return replaced ? plan.Report + " " + Strings.PreviousInRecycleBin : plan.Report;
    }

    // Write only what the user saw: there was a card and the document hasn't changed since.
    private void EnsureSeen(DocumentChange change, DocumentPlan plan)
    {
        if (!_previews.TryTake(change, out var fingerprint))
        {
            return;
        }

        if (fingerprint is null)
        {
            throw new AgentToolException(Strings.PreviewNotShown);
        }

        if (files.Fingerprint(plan.Target) != fingerprint)
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.ChangedSincePreview, plan.Target.Relative));
        }
    }
}
