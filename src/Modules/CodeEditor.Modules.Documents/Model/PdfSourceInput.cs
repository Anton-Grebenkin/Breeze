using System.ComponentModel;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>A PDF source to merge: the file and, optionally, its pages.</summary>
public sealed record PdfSourceInput(
    [property: Description("The PDF file relative to the workspace root.")] string Path,
    [property: Description("Pages to take, e.g. '1-3,7'; default all.")] string? Pages = null);
