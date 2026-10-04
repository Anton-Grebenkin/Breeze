using System.Collections.Frozen;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>
/// Document formats by extension. Legacy binary .doc, .xls and .ppt are not supported: an external app opens them.
/// </summary>
public static class DocumentKinds
{
    private static readonly FrozenDictionary<string, DocumentKind> ByExtension = new Dictionary<string, DocumentKind>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = DocumentKind.Pdf,
        [".docx"] = DocumentKind.Word,
        [".docm"] = DocumentKind.Word,
        [".xlsx"] = DocumentKind.Excel,
        [".xlsm"] = DocumentKind.Excel,
        [".pptx"] = DocumentKind.PowerPoint,
        [".pptm"] = DocumentKind.PowerPoint,
        [".csv"] = DocumentKind.Csv,
        [".tsv"] = DocumentKind.Csv,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static DocumentKind Of(string path) =>
        ByExtension.TryGetValue(Path.GetExtension(path), out var kind) ? kind : DocumentKind.None;

    /// <summary>Comma-separated extensions for messages to the model.</summary>
    public static string Supported => string.Join(", ", ByExtension.Keys.Order(StringComparer.Ordinal));
}
