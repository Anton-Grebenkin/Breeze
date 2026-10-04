namespace CodeEditor.Modules.Documents.Model;

/// <summary>Document format by file extension (<see cref="DocumentKinds"/>).</summary>
public enum DocumentKind
{
    None,
    Pdf,

    /// <summary>Word: .docx, .docm.</summary>
    Word,

    /// <summary>Excel: .xlsx, .xlsm.</summary>
    Excel,

    /// <summary>PowerPoint: .pptx, .pptm.</summary>
    PowerPoint,

    /// <summary>Comma-, semicolon- or tab-separated values: .csv, .tsv.</summary>
    Csv,
}
