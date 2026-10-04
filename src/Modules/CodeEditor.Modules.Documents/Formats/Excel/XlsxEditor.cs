using System.Globalization;
using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Edits an Excel workbook in place: only the touched cells and the sheet list change; charts, pivot tables, formatting
/// and everything the editor does not understand stay as they were. The calculation chain is deleted and the workbook
/// is marked for full recalculation on load: otherwise Excel shows stale formula values or offers to repair the file.
/// </summary>
internal static class XlsxEditor
{
    /// <summary>Writes cell values (a <c>null</c> <paramref name="sheet"/> means the first visible sheet).</summary>
    /// <exception cref="AgentToolException">The sheet is missing or a value is invalid.</exception>
    public static byte[] SetCells(byte[] bytes, string? sheet, IReadOnlyList<(CellAddress Address, JsonElement Value)> cells) => Edit(bytes, workbook =>
    {
        var part = Worksheet(workbook, sheet);
        var worksheet = part.Worksheet ?? throw OpenXmlFiles.NotDocument();
        var data = worksheet.GetFirstChild<SheetData>() ?? worksheet.AppendChild(new SheetData());
        var rows = new SheetRows(data);
        var shared = new SharedFormulas(data);
        var tables = new SheetTables(part);
        foreach (var (address, value) in cells)
        {
            var header = tables.Header(address, value);
            var cell = rows.Cell(address);
            shared.Expand(cell);
            XlsxCells.Write(cell, header is null ? value : JsonSerializer.SerializeToElement("'" + header));
        }

        UpdateDimension(worksheet, data);
    });

    /// <exception cref="AgentToolException">The sheet name is invalid or taken.</exception>
    public static byte[] AddSheet(byte[] bytes, SheetInput input) => Edit(bytes, workbook =>
    {
        var list = workbook.Workbook!.Sheets ??= new Sheets();
        var existing = list.Elements<Sheet>().ToList();
        var named = input with { Name = SheetNames.Validate(input.Name, existing.Select(item => item.Name?.Value ?? string.Empty)) };
        var id = existing.Select(item => item.SheetId?.Value ?? 0).DefaultIfEmpty(0U).Max() + 1;
        XlsxWriter.AddSheet(workbook, list, named, id, XlsxStyles.EnsureBold(workbook), selected: false);
    });

    /// <summary>Renames a sheet and the references to it in formulas, defined names and charts.</summary>
    /// <exception cref="AgentToolException">The sheet is missing, or the new name is invalid or taken.</exception>
    public static byte[] RenameSheet(byte[] bytes, string sheet, string newName) => Edit(bytes, workbook =>
    {
        var target = FindSheet(workbook, sheet);
        var oldName = target.Name!.Value!;
        var others = workbook.Workbook!.Sheets!.Elements<Sheet>().Where(item => !ReferenceEquals(item, target)).Select(item => item.Name?.Value ?? string.Empty);
        var name = SheetNames.Validate(newName, others);
        target.Name = name;
        SheetReferences.Rename(workbook, oldName, name);
    });

    public static IReadOnlyList<string> SheetNamesOf(WorkbookPart workbook) =>
        [.. workbook.Workbook?.Sheets?.Elements<Sheet>().Select(sheet => sheet.Name?.Value ?? string.Empty) ?? []];

    private static WorksheetPart Worksheet(WorkbookPart workbook, string? sheet)
    {
        var target = FindSheet(workbook, sheet);
        return target.Id?.Value is { } id && workbook.TryGetPartById(id, out var part) && part is WorksheetPart worksheet
            ? worksheet
            : throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.NotWorksheet, target.Name?.Value));
    }

    // Without a name, the first visible sheet: the one the user sees, not a hidden settings sheet.
    private static Sheet FindSheet(WorkbookPart workbook, string? name)
    {
        var sheets = workbook.Workbook?.Sheets?.Elements<Sheet>().ToList() ?? [];
        var sheet = string.IsNullOrWhiteSpace(name)
            ? sheets.FirstOrDefault(item => item.State?.Value is not { } state || state == SheetStateValues.Visible) ?? sheets.FirstOrDefault()
            : sheets.FirstOrDefault(item => string.Equals(item.Name?.Value, name.Trim(), StringComparison.OrdinalIgnoreCase));
        return sheet ?? throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.SheetNotFound, name, string.Join(", ", SheetNamesOf(workbook))));
    }

    // Updates the stored sheet dimension, if the file has one, to the actual cells.
    private static void UpdateDimension(Worksheet worksheet, SheetData data)
    {
        if (worksheet.SheetDimension is not { } dimension)
        {
            return;
        }

        var addresses = data.Elements<Row>().SelectMany(row => row.Elements<Cell>())
            .Select(cell => CellAddress.TryParse(cell.CellReference?.Value, out var address) ? address : (CellAddress?)null)
            .OfType<CellAddress>()
            .ToList();
        dimension.Reference = addresses.Count == 0
            ? "A1"
            : new CellRange(new CellAddress(addresses.Min(item => item.Row), addresses.Min(item => item.Column)), new CellAddress(addresses.Max(item => item.Row), addresses.Max(item => item.Column))).ToString();
    }

    private static byte[] Edit(byte[] bytes, Action<WorkbookPart> change)
    {
        using var stream = OpenXmlFiles.Editable(bytes);
        using (var document = OpenXmlFiles.OpenExcel(stream, editable: true))
        {
            var workbook = document.WorkbookPart ?? throw OpenXmlFiles.NotDocument();
            change(workbook);
            Recalculate(workbook);
        }

        return stream.ToArray();
    }

    private static void Recalculate(WorkbookPart workbook)
    {
        if (workbook.CalculationChainPart is { } chain)
        {
            workbook.DeletePart(chain);
        }

        var root = workbook.Workbook!;
        root.CalculationProperties ??= new CalculationProperties();
        root.CalculationProperties.FullCalculationOnLoad = true;
    }
}
