using System.Globalization;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>
/// A non-empty sheet cell. <see cref="Value"/> is culture-invariant text: a number with a dot, an ISO date, TRUE/FALSE;
/// <see cref="Formula"/> is the formula without "=", if any.
/// </summary>
/// <param name="Row">1-based row.</param>
/// <param name="Column">1-based column: A is 1.</param>
public readonly record struct SpreadsheetCell(int Row, int Column, CellKind Kind, string Value)
{
    public string? Formula { get; init; }

    /// <summary>
    /// Numeric value; for dates, times and durations the Excel serial number: days since the workbook's epoch (1900 or 1904).
    /// </summary>
    public double Number { get; init; }

    public CellAddress Address => new(Row, Column);

    /// <summary>Value for display: a number in the given culture, anything else as is.</summary>
    public string DisplayValue(CultureInfo culture) => Kind == CellKind.Number ? Number.ToString(culture) : Value;
}
