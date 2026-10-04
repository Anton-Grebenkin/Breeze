namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>Kind of a formula text segment (<see cref="FormulaSegments"/>).</summary>
internal enum FormulaSegmentKind
{
    /// <summary>References, functions, numbers, operators.</summary>
    Plain,

    /// <summary>A quoted string.</summary>
    String,

    /// <summary>A sheet name in apostrophes.</summary>
    SheetName,

    /// <summary>In square brackets: a table column or an external workbook index.</summary>
    Bracket,
}
