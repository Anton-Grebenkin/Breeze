namespace CodeEditor.Modules.Documents.Services.Changes;

/// <summary>Actions of the <c>document_change</c> tool.</summary>
public static class DocumentActions
{
    public const string CreateDocx = "create_docx";
    public const string CreatePdf = "create_pdf";
    public const string CreateXlsx = "create_xlsx";
    public const string ReplaceText = "replace_text";
    public const string Insert = "insert";
    public const string SetCells = "set_cells";
    public const string AddSheet = "add_sheet";
    public const string RenameSheet = "rename_sheet";
    public const string MergePdf = "merge_pdf";
    public const string ExtractPages = "extract_pages";

    public static IReadOnlyList<string> All { get; } =
        [CreateDocx, CreatePdf, CreateXlsx, ReplaceText, Insert, SetCells, AddSheet, RenameSheet, MergePdf, ExtractPages];

    /// <summary>The action creates a whole file: an existing file is replaced only with <c>overwrite</c>.</summary>
    public static bool Creates(string action) => action is CreateDocx or CreatePdf or CreateXlsx or MergePdf or ExtractPages;
}
