using System.Globalization;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>
/// Document blocks to Word elements for a specific document: its styles (<see cref="WordStyleIds"/>), its numbering
/// for lists (<see cref="WordLists"/>), relationships of its main part for links. Used both when creating a document
/// and when inserting into an existing one.
/// </summary>
internal sealed class WordBlocks(MainDocumentPart main, WordStyleIds styles, WordLists lists)
{
    /// <summary>Full-width table: 5000 is 100 percent in fiftieths of a percent.</summary>
    private const string FullWidth = "5000";
    private const int TextWidthTwips = 9638;

    // Current ordered list instance and last number per level.
    private readonly Dictionary<int, (int Instance, int Number)> _ordered = [];

    public IEnumerable<OpenXmlElement> Convert(RichDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        DocumentBlock? previous = null;
        foreach (var block in document.Blocks)
        {
            if (block.Kind != DocumentBlockKind.ListItem)
            {
                _ordered.Clear();
            }

            foreach (var element in Convert(block, previous))
            {
                yield return element;
            }

            previous = block;
        }
    }

    private IEnumerable<OpenXmlElement> Convert(DocumentBlock block, DocumentBlock? previous) => block.Kind switch
    {
        DocumentBlockKind.Heading => [Paragraph(Properties(styles.Heading(block.Level), block.Alignment), block.Runs)],
        DocumentBlockKind.Slide => [Paragraph(Properties(styles.Heading(2), BlockAlignment.Left), block.Runs)],
        DocumentBlockKind.ListItem => [ListItem(block, previous)],
        DocumentBlockKind.Table when block.Table is { } table => [Table(table)],
        DocumentBlockKind.Code => block.PlainText.TrimEnd('\n').Split('\n').Select(line => Paragraph(Properties(styles.Code, BlockAlignment.Left), [new TextRun(line.TrimEnd('\r'))])),
        DocumentBlockKind.Quote or DocumentBlockKind.Notes => [Paragraph(Properties(styles.Quote, block.Alignment), block.Runs)],
        DocumentBlockKind.Rule => [Rule()],
        _ => [Paragraph(Properties(null, block.Alignment), block.Runs)],
    };

    private Paragraph ListItem(DocumentBlock block, DocumentBlock? previous)
    {
        var level = Math.Clamp(block.Level, 0, 8);
        var instance = lists.BulletInstance;
        if (block.IsOrdered)
        {
            // Start a new list unless the number continues the previous item at this level.
            var continues = previous?.Kind == DocumentBlockKind.ListItem && _ordered.TryGetValue(level, out var current) && block.Number > current.Number;
            instance = continues ? _ordered[level].Instance : lists.NewOrdered(block.Number);
            _ordered[level] = (instance, block.Number);
        }

        var properties = Properties(styles.ListParagraph, BlockAlignment.Left);
        properties.Append(new NumberingProperties(new NumberingLevelReference { Val = level }, new NumberingId { Val = instance }));
        return Paragraph(properties, block.Runs);
    }

    private Table Table(DocumentTable source)
    {
        var columns = Math.Max(source.ColumnCount, 1);
        var table = new Table(
            new TableProperties(
                new TableStyle { Val = styles.Table },
                new TableWidth { Width = FullWidth, Type = TableWidthUnitValues.Pct },
                new TableLook { FirstRow = true, NoVerticalBand = true, Val = "04A0" }),
            new TableGrid(Enumerable.Range(0, columns).Select(_ => new GridColumn { Width = (TextWidthTwips / columns).ToString(CultureInfo.InvariantCulture) })));
        for (var row = 0; row < source.Rows.Length; row++)
        {
            var header = row == 0;
            var tableRow = new TableRow();
            if (header)
            {
                tableRow.Append(new TableRowProperties(new TableHeader()));
            }

            for (var column = 0; column < columns; column++)
            {
                var runs = column < source.Rows[row].Length ? source.Rows[row][column] : [];
                var cellRuns = header ? runs.Select(run => run with { Style = run.Style | TextStyle.Bold }) : runs;
                tableRow.Append(new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto }), Paragraph(new ParagraphProperties(), cellRuns)));
            }

            table.Append(tableRow);
        }

        return table;
    }

    private static ParagraphProperties Properties(string? style, BlockAlignment alignment)
    {
        var properties = new ParagraphProperties();
        if (style is not null)
        {
            properties.Append(new ParagraphStyleId { Val = style });
        }

        if (Justification(alignment) is { } justification)
        {
            properties.Append(new Justification { Val = justification });
        }

        return properties;
    }

    private static JustificationValues? Justification(BlockAlignment alignment) => alignment switch
    {
        BlockAlignment.Center => JustificationValues.Center,
        BlockAlignment.Right => JustificationValues.Right,
        BlockAlignment.Justify => JustificationValues.Both,
        _ => null,
    };

    private static Paragraph Rule() => new(new ParagraphProperties(new ParagraphBorders(
        new BottomBorder { Val = BorderValues.Single, Size = 6, Space = 1, Color = "auto" })));

    private Paragraph Paragraph(ParagraphProperties properties, IEnumerable<TextRun> runs)
    {
        var paragraph = new Paragraph(properties);
        foreach (var run in runs)
        {
            paragraph.Append(run.Link is { } link && Uri.TryCreate(link, UriKind.Absolute, out var url) ? Hyperlink(url, run) : Run(run, hyperlink: false));
        }

        return paragraph;
    }

    private Hyperlink Hyperlink(Uri url, TextRun run) =>
        new(Run(run, hyperlink: true)) { Id = main.AddHyperlinkRelationship(url, isExternal: true).Id };

    // A line feed inside a run becomes a line break, a tab a tab character.
    private Run Run(TextRun source, bool hyperlink)
    {
        var run = new Run(RunProperties(source, hyperlink));
        var lines = source.Text.Split('\n');
        for (var line = 0; line < lines.Length; line++)
        {
            if (line > 0)
            {
                run.Append(new Break());
            }

            var parts = lines[line].TrimEnd('\r').Split('\t');
            for (var part = 0; part < parts.Length; part++)
            {
                if (part > 0)
                {
                    run.Append(new TabChar());
                }

                if (parts[part].Length > 0)
                {
                    run.Append(new Text(parts[part]) { Space = SpaceProcessingModeValues.Preserve });
                }
            }
        }

        return run;
    }

    private RunProperties RunProperties(TextRun run, bool hyperlink)
    {
        var properties = new RunProperties();
        if (hyperlink)
        {
            properties.Append(new RunStyle { Val = styles.Hyperlink });
        }

        if (run.Has(TextStyle.Code))
        {
            properties.Append(new RunFonts { Ascii = WordStyles.CodeFont, HighAnsi = WordStyles.CodeFont, ComplexScript = WordStyles.CodeFont });
        }

        if (run.Has(TextStyle.Bold))
        {
            properties.Append(new Bold());
        }

        if (run.Has(TextStyle.Italic))
        {
            properties.Append(new Italic());
        }

        if (run.Has(TextStyle.Strike))
        {
            properties.Append(new Strike());
        }

        if (run.Has(TextStyle.Underline))
        {
            properties.Append(new Underline { Val = UnderlineValues.Single });
        }

        return properties;
    }
}
