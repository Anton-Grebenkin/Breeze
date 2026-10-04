using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Expands shared formulas before a cell is written: if the cell belongs to a shared formula group, the whole group
/// becomes ordinary formulas with shifted references. Otherwise writing the group's first cell would leave the rest
/// without a formula, and Excel would report the file as damaged. The sheet is scanned once, on the first group met.
/// </summary>
internal sealed class SharedFormulas(SheetData data)
{
    private Dictionary<uint, List<Cell>>? _groups;

    public void Expand(Cell cell)
    {
        if (cell.CellFormula is not { } formula || formula.FormulaType?.Value != CellFormulaValues.Shared || formula.SharedIndex?.Value is not { } group)
        {
            return;
        }

        _groups ??= Collect();
        if (!_groups.Remove(group, out var cells))
        {
            return;
        }

        var origin = cells.FirstOrDefault(item => !string.IsNullOrEmpty(item.CellFormula!.Text));
        var originAddress = Address(origin);
        var text = origin?.CellFormula!.Text;
        // A formula that cannot be restored (no group origin) is replaced by its cached value.
        foreach (var item in cells)
        {
            var expanded = text is null || originAddress is not { } start || Address(item) is not { } current
                ? null
                : FormulaReferences.Shift(text, current.Row - start.Row, current.Column - start.Column);
            item.CellFormula = expanded is null ? null : new CellFormula(expanded);
        }
    }

    private Dictionary<uint, List<Cell>> Collect()
    {
        var groups = new Dictionary<uint, List<Cell>>();
        foreach (var cell in data.Descendants<Cell>())
        {
            if (cell.CellFormula is { } formula && formula.FormulaType?.Value == CellFormulaValues.Shared && formula.SharedIndex?.Value is { } group)
            {
                if (!groups.TryGetValue(group, out var cells))
                {
                    cells = [];
                    groups[group] = cells;
                }

                cells.Add(cell);
            }
        }

        return groups;
    }

    private static CellAddress? Address(Cell? cell) =>
        cell?.CellReference?.Value is { } reference && CellAddress.TryParse(reference, out var address) ? address : null;
}
