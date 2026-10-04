using System.Globalization;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using MigraDoc.DocumentObjectModel;

namespace CodeEditor.Modules.Documents.Formats.Pdf;

/// <summary>
/// Document blocks to a MigraDoc document for PDF rendering: headings, paragraphs, lists numbered from the text, tables
/// with the header row repeated on each page, code, quotes, rules; page numbers in the footer. Fonts are Segoe UI and
/// Consolas (<see cref="WindowsFontResolver"/>), so Cyrillic renders.
/// </summary>
internal sealed class MigraDocBlocks
{
    private const string TextFont = "Segoe UI";
    private const string CodeFont = "Consolas";
    private const string CodeStyle = "Code";
    private const string QuoteStyle = "Quote";
    private const double TextWidthCm = 17;
    private const double ListIndentCm = 0.6;
    private static readonly string[] Bullets = ["•", "–", "·"];
    private static readonly double[] HeadingSizes = [18, 15, 13, 12, 11, 11];

    private readonly Document _document = new();
    private readonly Section _section;

    private MigraDocBlocks()
    {
        DefineStyles();
        _section = _document.AddSection();
        _section.PageSetup.PageFormat = PageFormat.A4;
        _section.PageSetup.TopMargin = _section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        _section.PageSetup.LeftMargin = _section.PageSetup.RightMargin = Unit.FromCentimeter(2);
        var footer = _section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddPageField();
    }

    public static Document Create(RichDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var builder = new MigraDocBlocks();
        foreach (var block in source.Blocks)
        {
            builder.Add(block);
        }

        if (source.Blocks.FirstOrDefault(block => block.Kind == DocumentBlockKind.Heading) is { } title)
        {
            builder._document.Info.Title = title.PlainText;
        }

        return builder._document;
    }

    private void DefineStyles()
    {
        var normal = _document.Styles[StyleNames.Normal]!;
        normal.Font.Name = TextFont;
        normal.Font.Size = 10.5;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        for (var level = 1; level <= HeadingSizes.Length; level++)
        {
            var heading = _document.Styles["Heading" + level.ToString(CultureInfo.InvariantCulture)]!;
            heading.Font.Size = HeadingSizes[level - 1];
            heading.Font.Bold = true;
            heading.ParagraphFormat.SpaceBefore = Unit.FromPoint(level == 1 ? 14 : 10);
            heading.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);
            heading.ParagraphFormat.KeepWithNext = true;
        }

        var code = _document.Styles.AddStyle(CodeStyle, StyleNames.Normal);
        code.Font.Name = CodeFont;
        code.Font.Size = 9;
        code.ParagraphFormat.Shading.Color = Colors.WhiteSmoke;
        code.ParagraphFormat.LeftIndent = Unit.FromCentimeter(0.2);
        var quote = _document.Styles.AddStyle(QuoteStyle, StyleNames.Normal);
        quote.Font.Italic = true;
        quote.Font.Color = Colors.DimGray;
        quote.ParagraphFormat.LeftIndent = Unit.FromCentimeter(0.8);
    }

    private void Add(DocumentBlock block)
    {
        switch (block.Kind)
        {
            case DocumentBlockKind.Heading:
                AddRuns(_section.AddParagraph(), block.Runs).Style = "Heading" + Math.Clamp(block.Level, 1, HeadingSizes.Length).ToString(CultureInfo.InvariantCulture);
                break;
            case DocumentBlockKind.Slide:
                var slide = _section.AddParagraph(string.Format(CultureInfo.CurrentCulture, Strings.SlideNumber, block.Number) + (block.Runs.IsEmpty ? string.Empty : ": "));
                AddRuns(slide, block.Runs).Style = StyleNames.Heading2;
                break;
            case DocumentBlockKind.ListItem:
                AddListItem(block);
                break;
            case DocumentBlockKind.Table when block.Table is { } table:
                AddTable(table);
                break;
            case DocumentBlockKind.Code:
                AddCode(block.PlainText);
                break;
            case DocumentBlockKind.Quote or DocumentBlockKind.Notes:
                AddRuns(_section.AddParagraph(), block.Runs).Style = QuoteStyle;
                break;
            case DocumentBlockKind.Rule:
                _section.AddParagraph().Format.Borders.Bottom.Width = Unit.FromPoint(0.75);
                break;
            default:
                Aligned(AddRuns(_section.AddParagraph(), block.Runs), block.Alignment);
                break;
        }
    }

    // The number or bullet is text with a hanging indent: the number comes from Markdown, not recounted.
    private void AddListItem(DocumentBlock block)
    {
        var level = Math.Clamp(block.Level, 0, 8);
        var marker = block.IsOrdered ? block.Number.ToString(CultureInfo.InvariantCulture) + "." : Bullets[level % Bullets.Length];
        var paragraph = _section.AddParagraph();
        paragraph.Format.LeftIndent = Unit.FromCentimeter(ListIndentCm * (level + 1));
        paragraph.Format.FirstLineIndent = Unit.FromCentimeter(-ListIndentCm);
        paragraph.Format.SpaceAfter = Unit.FromPoint(2);
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(ListIndentCm * (level + 1)));
        paragraph.AddText(marker);
        paragraph.AddTab();
        AddRuns(paragraph, block.Runs);
    }

    private void AddCode(string text)
    {
        var paragraph = _section.AddParagraph();
        paragraph.Style = CodeStyle;
        var lines = text.TrimEnd('\n').Split('\n');
        for (var line = 0; line < lines.Length; line++)
        {
            if (line > 0)
            {
                paragraph.AddLineBreak();
            }

            // Non-breaking spaces keep code indentation.
            paragraph.AddText(lines[line].TrimEnd('\r').Replace(' ', '\u00A0').Replace("\t", "\u00A0\u00A0\u00A0\u00A0", StringComparison.Ordinal));
        }
    }

    private void AddTable(DocumentTable source)
    {
        var columns = Math.Max(source.ColumnCount, 1);
        var table = _section.AddTable();
        table.Borders.Width = Unit.FromPoint(0.5);
        table.Borders.Color = Colors.Gray;
        table.Format.Font.Size = 9.5;
        foreach (var width in ColumnWidths(source, columns))
        {
            table.AddColumn(Unit.FromCentimeter(width));
        }

        for (var index = 0; index < source.Rows.Length; index++)
        {
            var row = table.AddRow();
            if (index == 0)
            {
                row.HeadingFormat = true;
                row.Format.Font.Bold = true;
                row.Shading.Color = Colors.Gainsboro;
            }

            for (var column = 0; column < columns && column < source.Rows[index].Length; column++)
            {
                AddRuns(row.Cells[column].AddParagraph(), source.Rows[index][column]);
            }
        }

        _section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(2);
    }

    // Column width follows its text length, clamped to 4–40 characters so one column cannot squeeze the others.
    private static IEnumerable<double> ColumnWidths(DocumentTable table, int columns)
    {
        var weights = Enumerable.Range(0, columns)
            .Select(column => (double)Math.Clamp(Enumerable.Range(0, table.Rows.Length).Max(row => table.CellText(row, column).Length), 4, 40))
            .ToList();
        var total = weights.Sum();
        return weights.Select(weight => TextWidthCm * weight / total);
    }

    private static Paragraph Aligned(Paragraph paragraph, BlockAlignment alignment)
    {
        paragraph.Format.Alignment = alignment switch
        {
            BlockAlignment.Center => ParagraphAlignment.Center,
            BlockAlignment.Right => ParagraphAlignment.Right,
            BlockAlignment.Justify => ParagraphAlignment.Justify,
            _ => ParagraphAlignment.Left,
        };
        return paragraph;
    }

    private static Paragraph AddRuns(Paragraph paragraph, IEnumerable<TextRun> runs)
    {
        foreach (var run in runs)
        {
            var lines = run.Text.Replace("\t", "    ", StringComparison.Ordinal).Split('\n');
            for (var line = 0; line < lines.Length; line++)
            {
                if (line > 0)
                {
                    paragraph.AddLineBreak();
                }

                if (lines[line].Length > 0)
                {
                    Format(Text(paragraph, lines[line].TrimEnd('\r'), run.Link), run);
                }
            }
        }

        return paragraph;
    }

    private static FormattedText Text(Paragraph paragraph, string text, string? link) =>
        link is not null && Uri.TryCreate(link, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" or "mailto"
            ? paragraph.AddHyperlink(url.AbsoluteUri, HyperlinkType.Web).AddFormattedText(text)
            : paragraph.AddFormattedText(text);

    private static void Format(FormattedText text, TextRun run)
    {
        text.Bold = run.Has(TextStyle.Bold);
        text.Italic = run.Has(TextStyle.Italic);
        if (run.Has(TextStyle.Underline) || run.Link is not null)
        {
            text.Underline = Underline.Single;
        }

        if (run.Link is not null)
        {
            text.Color = Colors.Blue;
        }

        if (run.Has(TextStyle.Code))
        {
            text.Font.Name = CodeFont;
        }
    }
}
