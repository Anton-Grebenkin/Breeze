using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Sheet rows for writing cells: finds or inserts a row and a cell in place, since Excel requires rows in ascending
/// order and cells in column order. Row numbers and cell references the file omitted are set explicitly. Rows are
/// looked up in a dictionary; a new row's position is found by binary search.
/// </summary>
internal sealed class SheetRows
{
    private readonly SheetData _data;
    private readonly Dictionary<uint, Row> _rows = [];
    private readonly List<uint> _numbers = [];

    public SheetRows(SheetData data)
    {
        _data = data;
        uint previous = 0;
        foreach (var row in data.Elements<Row>())
        {
            var number = row.RowIndex?.Value ?? previous + 1;
            row.RowIndex = number;
            previous = number;
            if (_rows.TryAdd(number, row))
            {
                _numbers.Add(number);
            }
        }

        _numbers.Sort();
    }

    public Cell Cell(CellAddress address)
    {
        var row = Row((uint)address.Row);
        var reference = address.ToString();
        var previous = 0;
        foreach (var cell in row.Elements<Cell>())
        {
            var column = cell.CellReference?.Value is { } existing && CellAddress.TryParse(existing, out var parsed) ? parsed.Column : previous + 1;
            cell.CellReference ??= new CellAddress(address.Row, column).ToString();
            previous = column;
            if (column == address.Column)
            {
                return cell;
            }

            if (column > address.Column)
            {
                return cell.InsertBeforeSelf(new Cell { CellReference = reference });
            }
        }

        // The row's column spans would be stale after the insert; Excel recomputes them.
        row.Spans = null;
        return row.AppendChild(new Cell { CellReference = reference });
    }

    private Row Row(uint number)
    {
        if (_rows.TryGetValue(number, out var existing))
        {
            existing.Spans = null;
            return existing;
        }

        var row = new Row { RowIndex = number };
        var position = _numbers.BinarySearch(number);
        position = position >= 0 ? position : ~position;
        if (position < _numbers.Count)
        {
            _rows[_numbers[position]].InsertBeforeSelf(row);
        }
        else
        {
            _data.Append(row);
        }

        _numbers.Insert(position, number);
        _rows[number] = row;
        return row;
    }
}
