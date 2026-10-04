using System.Globalization;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Workbook cell values: shared strings, numbers, booleans, errors, dates and durations (numbers with a date or time
/// format). The shared string table, formats and date system are read once per workbook.
/// </summary>
internal sealed class XlsxValues
{
    // 1904 date system (workbooks from Excel for Mac): its serial 0 is 1 January 1904, which is 1462 in the 1900 system.
    private const double Date1904Offset = 1462;

    // The 1900 system treats 1900 as a leap year, so before 1 March 1900 (serial 61) its dates are a day ahead of the
    // OLE dates used for conversion.
    private const double FirstMarch1900 = 61;

    // Excel itself shows nothing beyond 31 December 9999.
    private const double MaxSerial = 2958466;

    private readonly string[] _strings;
    private readonly NumberKind[] _styles;
    private readonly double _dateOffset;

    private XlsxValues(string[] strings, NumberKind[] styles, double dateOffset)
    {
        _strings = strings;
        _styles = styles;
        _dateOffset = dateOffset;
    }

    public static XlsxValues Load(WorkbookPart workbook)
    {
        var strings = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(ItemText).ToArray() ?? [];
        var stylesheet = workbook.WorkbookStylesPart?.Stylesheet;
        var codes = stylesheet?.NumberingFormats?.Elements<NumberingFormat>()
            .Where(format => format.NumberFormatId?.Value is not null)
            .GroupBy(format => format.NumberFormatId!.Value)
            .ToDictionary(group => group.Key, group => group.First().FormatCode?.Value) ?? [];
        var styles = stylesheet?.CellFormats?.Elements<CellFormat>()
            .Select(format => format.NumberFormatId?.Value ?? 0)
            .Select(id => NumberFormats.Classify(id, codes.GetValueOrDefault(id)))
            .ToArray() ?? [];
        var date1904 = workbook.Workbook?.WorkbookProperties?.Date1904?.Value == true;
        return new XlsxValues(strings, styles, date1904 ? Date1904Offset : 0);
    }

    /// <summary>The model cell, or <c>null</c> for an empty cell without a formula.</summary>
    public SpreadsheetCell? Read(Cell cell, CellAddress address, string? formula)
    {
        var raw = cell.CellValue?.Text;
        var type = cell.DataType?.Value;
        if (type == CellValues.InlineString)
        {
            return TextCell(address, cell.InlineString is { } inline ? ItemText(inline) : raw ?? string.Empty, formula);
        }

        if (raw is null)
        {
            return formula is null ? null : new SpreadsheetCell(address.Row, address.Column, CellKind.Text, string.Empty) { Formula = formula };
        }

        if (type == CellValues.SharedString)
        {
            return TextCell(address, int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < _strings.Length ? _strings[index] : raw, formula);
        }

        if (type == CellValues.Boolean)
        {
            return new SpreadsheetCell(address.Row, address.Column, CellKind.Boolean, raw is "1" or "true" ? "TRUE" : "FALSE") { Formula = formula };
        }

        if (type == CellValues.Error)
        {
            return new SpreadsheetCell(address.Row, address.Column, CellKind.Error, raw) { Formula = formula };
        }

        if (type == CellValues.String || type == CellValues.Date || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return TextCell(address, raw, formula);
        }

        return Number(address, number, KindOf(cell), formula);
    }

    private SpreadsheetCell Number(CellAddress address, double number, NumberKind kind, string? formula)
    {
        var text = kind switch
        {
            NumberKind.Date => DateText(number),
            NumberKind.Time => TimeText(number),
            NumberKind.Duration => DurationText(number),
            _ => null,
        };
        return text is null
            ? new SpreadsheetCell(address.Row, address.Column, CellKind.Number, number.ToString(CultureInfo.InvariantCulture)) { Number = number, Formula = formula }
            : new SpreadsheetCell(address.Row, address.Column, CellKind.Date, text) { Number = number, Formula = formula };
    }

    private NumberKind KindOf(Cell cell) =>
        cell.StyleIndex?.Value is { } index && index < _styles.Length ? _styles[index] : NumberKind.Number;

    // "2026-10-03", with a time "2026-10-03 14:30"; null (shown as a number) outside the date range.
    private string? DateText(double number)
    {
        var serial = number + _dateOffset;
        if (_dateOffset == 0 && serial is >= 1 and < FirstMarch1900)
        {
            serial++;
        }

        if (ToDateTime(serial) is not { } value)
        {
            return null;
        }

        return value.TimeOfDay == TimeSpan.Zero
            ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : value.ToString(value.Second == 0 ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    // Time of day only, "14:30:00": the fractional part of the serial.
    private static string? TimeText(double number) =>
        ToDateTime(number) is { } value ? value.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : null;

    // Total hours: a day and a half is "36:00:00", minus half an hour "-0:30:00". Seconds are rounded so binary
    // fractions don't turn 36 hours into "35:59:59".
    private static string? DurationText(double days)
    {
        if (!double.IsFinite(days) || Math.Abs(days) >= MaxSerial)
        {
            return null;
        }

        var span = TimeSpan.FromSeconds((long)Math.Round(Math.Abs(days) * TimeSpan.SecondsPerDay));
        var sign = days < 0 && span > TimeSpan.Zero ? "-" : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{span.Ticks / TimeSpan.TicksPerHour}:{span.Minutes:00}:{span.Seconds:00}");
    }

    private static DateTime? ToDateTime(double serial)
    {
        try
        {
            return DateTime.FromOADate(serial);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static SpreadsheetCell TextCell(CellAddress address, string text, string? formula) =>
        new(address.Row, address.Column, CellKind.Text, text) { Formula = formula };

    // A shared string is plain text or formatted runs; phonetic runs (rPh) are not text.
    private static string ItemText(DocumentFormat.OpenXml.OpenXmlElement item) =>
        string.Concat(item.ChildElements.Select(child => child switch
        {
            DocumentFormat.OpenXml.Spreadsheet.Text text => text.Text,
            Run run => run.Text?.Text ?? string.Empty,
            _ => string.Empty,
        }));
}
