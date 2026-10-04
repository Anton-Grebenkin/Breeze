using System.Collections.Frozen;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using static CodeEditor.Modules.Documents.Services.Changes.ChangePreviews;

namespace CodeEditor.Modules.Documents.Services.Changes;

/// <summary>
/// Builds a whole document edit in memory (<see cref="DocumentPlan"/>): the approval card comes from it and, once
/// approved, the file is written from it. The action checks the file format; creating over an existing file requires
/// <c>overwrite</c>. Parse and argument errors become messages the model understands.
/// </summary>
public sealed class DocumentChanges(DocumentFiles files)
{
    // The file type each action requires; creation only in the main format, without macros.
    private static readonly FrozenDictionary<string, (DocumentKind Kind, string? Extension)> Targets = new Dictionary<string, (DocumentKind, string?)>
    {
        [DocumentActions.CreateDocx] = (DocumentKind.Word, ".docx"),
        [DocumentActions.ReplaceText] = (DocumentKind.Word, null),
        [DocumentActions.Insert] = (DocumentKind.Word, null),
        [DocumentActions.CreatePdf] = (DocumentKind.Pdf, ".pdf"),
        [DocumentActions.MergePdf] = (DocumentKind.Pdf, ".pdf"),
        [DocumentActions.ExtractPages] = (DocumentKind.Pdf, ".pdf"),
        [DocumentActions.CreateXlsx] = (DocumentKind.Excel, ".xlsx"),
        [DocumentActions.SetCells] = (DocumentKind.Excel, null),
        [DocumentActions.AddSheet] = (DocumentKind.Excel, null),
        [DocumentActions.RenameSheet] = (DocumentKind.Excel, null),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private readonly WordChanges _word = new(files);
    private readonly SheetChanges _sheets = new(files);
    private readonly PdfChanges _pdf = new(files);

    /// <exception cref="AgentToolException">Invalid action, path or arguments, or the document can't be read.</exception>
    public DocumentPlan Plan(DocumentChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!Targets.TryGetValue(change.Action, out var expected))
        {
            throw new AgentToolException(Format(Strings.UnknownAction, change.Action, string.Join(", ", DocumentActions.All)));
        }

        var target = files.Resolve(change.Path, forWriting: true);
        if (target.Kind != expected.Kind || (expected.Extension is { } extension && !target.Full.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AgentToolException(Format(Strings.WrongFormatForAction, change.Action, expected.Extension ?? expected.Kind.ToString(), target.Relative));
        }

        if (DocumentActions.Creates(change.Action) && files.Exists(target) && !change.Overwrite)
        {
            throw new AgentToolException(Format(Strings.FileExists, target.Relative));
        }

        try
        {
            return Run(change, target);
        }
        catch (Exception exception) when (DocumentErrors.IsReadFailure(exception))
        {
            throw new AgentToolException(Format(Strings.DocumentNotReadable, target.Relative, exception.Message));
        }
    }

    private DocumentPlan Run(DocumentChange change, DocumentPath target) => change.Action switch
    {
        DocumentActions.CreateDocx => _word.Create(change, target),
        DocumentActions.ReplaceText => _word.Replace(change, target),
        DocumentActions.Insert => _word.Insert(change, target),
        DocumentActions.CreatePdf => _pdf.Create(change, target),
        DocumentActions.MergePdf => _pdf.Merge(change, target),
        DocumentActions.ExtractPages => _pdf.Extract(change, target),
        DocumentActions.CreateXlsx => _sheets.Create(change, target),
        DocumentActions.SetCells => _sheets.SetCells(change, target),
        DocumentActions.AddSheet => _sheets.AddSheet(change, target),
        _ => _sheets.RenameSheet(change, target),
    };
}
