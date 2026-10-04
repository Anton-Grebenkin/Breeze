using System.Text;
using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Formats.Excel;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using static CodeEditor.Modules.Documents.Services.Changes.ChangePreviews;

namespace CodeEditor.Modules.Documents.Services.Changes;

/// <summary>
/// Excel edits for <c>document_change</c>: new workbook, writing cells and ranges, new sheet, sheet rename. The card
/// shows cells "before → after" or the new sheet's table, as the model will read them later.
/// </summary>
internal sealed class SheetChanges(DocumentFiles files)
{
    public DocumentPlan Create(DocumentChange change, DocumentPath target)
    {
        if (change.Sheets.Count == 0)
        {
            throw new AgentToolException(Format(Strings.ArgumentRequired, change.Action, "sheets"));
        }

        var names = new List<string>();
        var sheets = change.Sheets.Select(sheet => sheet with { Name = Named(sheet.Name, names) }).ToList();
        var bytes = XlsxWriter.Create(sheets);
        var before = files.Exists(target) ? Previous(files.Read(target)) : null;
        return new DocumentPlan(target, bytes, Of(target, before, Book(XlsxReader.Read(bytes)), Strings.ApprovalExcelCreate),
            Format(Strings.WorkbookCreated, target.Relative, string.Join(", ", names)) + " " + Strings.FormulasRecalculated);
    }

    public DocumentPlan SetCells(DocumentChange change, DocumentPath target)
    {
        var values = Targets(change);
        var old = files.Read(target);
        var book = XlsxReader.Read(old);
        var sheet = Sheet(book, change.Sheet);
        var bytes = XlsxEditor.SetCells(old, sheet.Name, values);
        var current = sheet.Cells.ToDictionary(cell => cell.Address);
        var before = new StringBuilder();
        var after = new StringBuilder();
        foreach (var (address, value) in values.OrderBy(item => item.Address.Row).ThenBy(item => item.Address.Column))
        {
            before.Append(address).Append(": ").Append(current.TryGetValue(address, out var cell) ? SheetText.Show(cell) : Strings.EmptyCell).Append('\n');
            after.Append(address).Append(": ").Append(XlsxCells.Describe(value)).Append('\n');
        }

        return new DocumentPlan(target, bytes, Of(target, before.ToString(), after.ToString(), Strings.ApprovalExcelEdit),
            Format(Strings.CellsWritten, values.Count, sheet.Name, target.Relative) + " " + Strings.FormulasRecalculated);
    }

    public DocumentPlan AddSheet(DocumentChange change, DocumentPath target)
    {
        var name = Required(change.Sheet, "sheet", change.Action);
        var old = files.Read(target);
        var bytes = XlsxEditor.AddSheet(old, new SheetInput(name, change.Rows));
        var book = XlsxReader.Read(bytes);
        var added = book.Sheets[^1];
        return new DocumentPlan(target, bytes, Of(target, Format(Strings.SheetList, SheetText.SheetList(XlsxReader.Read(old))), Book(book, added), Strings.ApprovalExcelEdit),
            Format(Strings.SheetAdded, added.Name, target.Relative));
    }

    public DocumentPlan RenameSheet(DocumentChange change, DocumentPath target)
    {
        var sheet = Required(change.Sheet, "sheet", change.Action);
        var newName = Required(change.NewName, "newName", change.Action);
        var old = files.Read(target);
        var bytes = XlsxEditor.RenameSheet(old, sheet, newName);
        return new DocumentPlan(target, bytes,
            Of(target, Format(Strings.SheetList, SheetText.SheetList(XlsxReader.Read(old))), Format(Strings.SheetList, SheetText.SheetList(XlsxReader.Read(bytes))), Strings.ApprovalExcelEdit),
            Format(Strings.SheetRenamed, sheet, newName.Trim(), target.Relative));
    }

    // Cells from the address list and from the rows block at start; the last write to a cell wins.
    private static List<(CellAddress Address, JsonElement Value)> Targets(DocumentChange change)
    {
        var targets = new Dictionary<CellAddress, JsonElement>();
        foreach (var cell in change.Cells)
        {
            targets[Address(cell.Cell)] = cell.Value;
        }

        var start = string.IsNullOrWhiteSpace(change.Start) ? new CellAddress(1, 1) : Address(change.Start);
        for (var row = 0; row < change.Rows.Length; row++)
        {
            for (var column = 0; column < (change.Rows[row]?.Length ?? 0); column++)
            {
                targets[new CellAddress(start.Row + row, start.Column + column)] = change.Rows[row][column];
            }
        }

        return targets.Count == 0
            ? throw new AgentToolException(Format(Strings.ArgumentRequired, change.Action, "cells / rows"))
            : [.. targets.Select(pair => (pair.Key, pair.Value))];
    }

    private static CellAddress Address(string text) =>
        CellAddress.TryParse(text, out var address) ? address : throw new AgentToolException(Format(Strings.InvalidCellAddress, text));

    private static SpreadsheetSheet Sheet(Spreadsheet book, string? name) =>
        book.Find(name) ?? throw new AgentToolException(Format(Strings.SheetNotFound, name, string.Join(", ", book.Sheets.Select(sheet => sheet.Name))));

    private static string Named(string name, List<string> taken)
    {
        var valid = SheetNames.Validate(name, taken);
        taken.Add(valid);
        return valid;
    }

    private static string Book(Spreadsheet book) => string.Join("\n\n", book.Sheets.Select(sheet => SheetText.Table(sheet, range: null)));

    // The workbook being replaced, for the card: a damaged one shows as empty since it gets replaced anyway.
    private static string Previous(byte[] bytes)
    {
        try
        {
            return Book(XlsxReader.Read(bytes));
        }
        catch (Exception exception) when (DocumentErrors.IsReadFailure(exception))
        {
            return string.Empty;
        }
    }

    private static string Book(Spreadsheet book, SpreadsheetSheet sheet) =>
        Format(Strings.SheetList, SheetText.SheetList(book)) + "\n\n" + SheetText.Table(sheet, range: null);
}
