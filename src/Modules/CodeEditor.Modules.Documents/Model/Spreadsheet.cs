using System.Collections.Immutable;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>A spreadsheet (Excel workbook or CSV): sheets in order.</summary>
public sealed record Spreadsheet(ImmutableArray<SpreadsheetSheet> Sheets)
{
    /// <summary>
    /// Sheet by name, case-insensitive as in Excel; <c>null</c> gives the first visible sheet, the one Excel shows.
    /// </summary>
    public SpreadsheetSheet? Find(string? name) => name is null
        ? Sheets.FirstOrDefault(sheet => !sheet.IsHidden) ?? Sheets.FirstOrDefault()
        : Sheets.FirstOrDefault(sheet => string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase));
}
