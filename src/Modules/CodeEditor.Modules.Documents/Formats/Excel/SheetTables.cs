using System.Globalization;
using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Header cells of the sheet's tables (ListObject). A table column name is stored both in the header cell and in the
/// table definition; if they differ, Excel offers to repair the file. So a header only takes text, which also becomes
/// the column name; a number, formula or empty value there is an error for the model.
/// </summary>
internal sealed class SheetTables
{
    private readonly Dictionary<CellAddress, (Table Table, TableColumn Column)> _headers = [];

    public SheetTables(WorksheetPart worksheet)
    {
        foreach (var table in worksheet.TableDefinitionParts.Select(part => part.Table).OfType<Table>())
        {
            if ((table.HeaderRowCount?.Value ?? 1) == 0 || !CellRange.TryParse(table.Reference?.Value, out var range))
            {
                continue;
            }

            var column = range.Start.Column;
            foreach (var tableColumn in table.TableColumns?.Elements<TableColumn>() ?? [])
            {
                _headers[new CellAddress(range.Start.Row, column++)] = (table, tableColumn);
            }
        }
    }

    /// <summary>The text for a header cell, or <c>null</c> if the cell is not a table header.</summary>
    /// <exception cref="AgentToolException">The value is not text, or another table column has that name.</exception>
    public string? Header(CellAddress address, JsonElement value)
    {
        if (!_headers.TryGetValue(address, out var header))
        {
            return null;
        }

        var tableName = header.Table.DisplayName?.Value ?? header.Table.Name?.Value ?? string.Empty;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()!.TrimStart('\'') : null;
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith('='))
        {
            throw new AgentToolException(Format(Strings.TableHeaderNotText, address, tableName));
        }

        var taken = header.Table.TableColumns!.Elements<TableColumn>()
            .Any(column => !ReferenceEquals(column, header.Column) && string.Equals(column.Name?.Value, text, StringComparison.OrdinalIgnoreCase));
        if (taken)
        {
            throw new AgentToolException(Format(Strings.TableHeaderTaken, text, tableName));
        }

        header.Column.Name = text;
        return text;
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
