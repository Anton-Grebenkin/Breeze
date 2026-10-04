using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Cell formatting: a minimal stylesheet for a new workbook (normal and bold formats) and a bold format added to an
/// existing one, based on its default font so a new sheet's header uses the same font as the rest.
/// </summary>
internal static class XlsxStyles
{
    /// <summary>The bold format in a new workbook.</summary>
    public const uint BoldFormat = 1;

    private const string DefaultFontName = "Calibri";
    private const double DefaultFontSize = 11;

    public static Stylesheet Create() => new(
        new Fonts(DefaultFont(), BoldFont(DefaultFont())) { Count = 2 },
        new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
        new Borders(new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder())) { Count = 1 },
        new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 },
        new CellFormats(
            new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 },
            new CellFormat { NumberFormatId = 0, FontId = 1, FillId = 0, BorderId = 0, FormatId = 0, ApplyFont = true }) { Count = 2 },
        new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });

    /// <summary>Adds a bold format (the workbook's default font, bold) and returns its index.</summary>
    public static uint EnsureBold(WorkbookPart workbook)
    {
        var part = workbook.WorkbookStylesPart ?? workbook.AddNewPart<WorkbookStylesPart>();
        var stylesheet = part.Stylesheet ??= Create();
        var fonts = stylesheet.Fonts ??= new Fonts(DefaultFont()) { Count = 1 };
        var formats = stylesheet.CellFormats ??= new CellFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 }) { Count = 1 };
        var baseFont = fonts.Elements<Font>().FirstOrDefault() ?? DefaultFont();
        fonts.Append(BoldFont((Font)baseFont.CloneNode(true)));
        var fontId = (uint)fonts.Elements<Font>().Count() - 1;
        fonts.Count = fontId + 1;
        // Reference a cell style (xfId) only if the workbook has one: Excel treats a dangling reference as damage.
        var bold = new CellFormat { NumberFormatId = 0, FontId = fontId, FillId = 0, BorderId = 0, ApplyFont = true };
        if (stylesheet.CellStyleFormats?.HasChildren == true)
        {
            bold.FormatId = 0;
        }

        formats.Append(bold);
        var formatId = (uint)formats.Elements<CellFormat>().Count() - 1;
        formats.Count = formatId + 1;
        return formatId;
    }

    private static Font DefaultFont() => new(new FontSize { Val = DefaultFontSize }, new FontName { Val = DefaultFontName });

    // The schema requires Bold as the font's first child.
    private static Font BoldFont(Font font)
    {
        if (font.GetFirstChild<Bold>() is null)
        {
            font.PrependChild(new Bold());
        }

        return font;
    }
}
