using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ChartFormula = DocumentFormat.OpenXml.Drawing.Charts.Formula;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Rewrites references to a renamed sheet across the workbook, as Excel itself does: formulas in cells, conditional
/// formatting, data validation and tables, internal hyperlinks, defined names, chart series (chart sheets included) and
/// pivot cache sources. Otherwise references to the missing sheet would turn into #REF!, and Excel might offer to
/// repair the file.
/// </summary>
internal static class SheetReferences
{
    public static void Rename(WorkbookPart workbook, string oldName, string newName)
    {
        foreach (var worksheet in workbook.WorksheetParts)
        {
            RenameFormulas(worksheet.Worksheet, oldName, newName);
            foreach (var hyperlink in worksheet.Worksheet?.Descendants<Hyperlink>() ?? [])
            {
                if (hyperlink.Location?.Value is { } location)
                {
                    hyperlink.Location = FormulaReferences.RenameSheet(location, oldName, newName);
                }
            }

            foreach (var table in worksheet.TableDefinitionParts)
            {
                RenameFormulas(table.Table, oldName, newName);
            }

            RenameCharts(worksheet.DrawingsPart, oldName, newName);
        }

        foreach (var chartsheet in workbook.ChartsheetParts)
        {
            RenameCharts(chartsheet.DrawingsPart, oldName, newName);
        }

        RenameFormulas(workbook.Workbook?.DefinedNames, oldName, newName);
        foreach (var source in workbook.PivotTableCacheDefinitionParts.Select(part => part.PivotCacheDefinition?.CacheSource?.WorksheetSource).OfType<WorksheetSource>())
        {
            if (string.Equals(source.Sheet?.Value, oldName, StringComparison.OrdinalIgnoreCase))
            {
                source.Sheet = newName;
            }
        }
    }

    private static void RenameCharts(DrawingsPart? drawings, string oldName, string newName)
    {
        foreach (var chart in drawings?.ChartParts ?? [])
        {
            RenameFormulas(chart.ChartSpace, oldName, newName);
        }
    }

    // Formula text lives in elements of several types: cells, conditional formatting, validation, table columns, names.
    private static void RenameFormulas(OpenXmlElement? root, string oldName, string newName)
    {
        foreach (var formula in root?.Descendants<OpenXmlLeafTextElement>() ?? [])
        {
            if (formula is CellFormula or Formula or Formula1 or Formula2 or CalculatedColumnFormula or TotalsRowFormula or DefinedName or ChartFormula)
            {
                formula.Text = FormulaReferences.RenameSheet(formula.Text, oldName, newName);
            }
        }
    }
}
