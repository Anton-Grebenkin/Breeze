using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using CodeEditor.UI.Themes;
using WpfList = System.Windows.Documents.List;
using WpfTable = System.Windows.Documents.Table;

namespace CodeEditor.Modules.Documents.Wpf.Rendering;

/// <summary>
/// Word document or presentation blocks → read-only <see cref="FlowDocument"/>: leveled headings, aligned paragraphs,
/// nested lists numbered as in the document, tables with a highlighted first row, code, quotes, rules, slides with
/// separators. Fonts and colors are theme resources, like chat Markdown. O(n) in blocks. FlowDocument elements are
/// created and laid out on the UI thread only, so blocks are capped at <see cref="MaxBlocks"/> (a table row counts as a
/// block): a long document or table shows its beginning with a hint to open the file in an external app.
/// </summary>
internal sealed class FlowDocumentBuilder
{
    /// <summary>About 100 pages of text; more would noticeably slow down opening the tab.</summary>
    public const int MaxBlocks = 3000;

    private const double Spacing = 10;
    private const double TightSpacing = 2;
    private const double ListIndent = 20;
    private const double CellPadding = 6;
    private static readonly string[] HeadingSizes = ["FontSize.Markdown.H1", "FontSize.Markdown.H2", "FontSize.Markdown.H3"];

    private readonly FlowDocument _document = new() { PagePadding = new Thickness(24, 16, 24, 24), ColumnWidth = double.PositiveInfinity };

    // Open lists by nesting level: a level-N item goes into list N, nested in the last item of list N-1.
    private readonly List<(WpfList List, bool Ordered)> _lists = [];
    private int _budget = MaxBlocks;

    public static FlowDocument Build(RichDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var builder = new FlowDocumentBuilder();
        builder.Style();
        foreach (var block in source.Blocks)
        {
            if (!builder.TryAdd(block))
            {
                builder._document.Blocks.Add(Quote([new TextRun(Strings.DocumentTruncated)], prefix: null));
                break;
            }
        }

        return builder._document;
    }

    private void Style()
    {
        _document.SetResourceReference(TextElement.FontFamilyProperty, "Font.Ui");
        _document.SetResourceReference(TextElement.FontSizeProperty, "FontSize.Normal");
        _document.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.EditorForeground);
        _document.SetResourceReference(FlowDocument.BackgroundProperty, ThemeKeys.EditorBackground);
    }

    // Adds the block if the budget allows; a long table gets as many rows as fit. false means content was cut off.
    private bool TryAdd(DocumentBlock block)
    {
        if (_budget <= 0)
        {
            return false;
        }

        if (block.Kind != DocumentBlockKind.ListItem)
        {
            _lists.Clear();
        }

        switch (block.Kind)
        {
            case DocumentBlockKind.ListItem:
                AddListItem(block);
                break;
            case DocumentBlockKind.Table when block.Table is { } table:
                var rows = Math.Min(table.Rows.Length, _budget);
                _document.Blocks.Add(Table(table, rows));
                _budget -= Math.Max(rows, 1);
                return rows == table.Rows.Length;
            default:
                _document.Blocks.Add(Convert(block));
                break;
        }

        _budget--;
        return true;
    }

    private Block Convert(DocumentBlock block) => block.Kind switch
    {
        DocumentBlockKind.Heading => Heading(block.Runs, block.Level, block.Alignment),
        DocumentBlockKind.Slide => Slide(block),
        DocumentBlockKind.Code => Code(block.PlainText),
        DocumentBlockKind.Quote => Quote(block.Runs, prefix: null),
        DocumentBlockKind.Notes => Quote(block.Runs, Strings.NotesLabel + " "),
        DocumentBlockKind.Rule => Rule(),
        _ => Paragraph(block.Runs, block.Alignment, Spacing),
    };

    private static Paragraph Paragraph(IEnumerable<TextRun> runs, BlockAlignment alignment, double bottom)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, bottom), TextAlignment = Alignment(alignment) };
        FlowInlines.Add(paragraph.Inlines, runs);
        return paragraph;
    }

    private static Paragraph Heading(IEnumerable<TextRun> runs, int level, BlockAlignment alignment)
    {
        var heading = Paragraph(runs, alignment, Spacing / 2);
        heading.Margin = heading.Margin with { Top = Spacing };
        heading.FontWeight = FontWeights.SemiBold;
        heading.SetResourceReference(TextElement.FontSizeProperty, HeadingSizes[Math.Clamp(level, 1, HeadingSizes.Length) - 1]);
        if (level == 1)
        {
            heading.SetResourceReference(TextElement.FontFamilyProperty, "Font.Display");
        }

        return heading;
    }

    // A slide is a numbered heading with a top border, so slides read as separate pages.
    private Paragraph Slide(DocumentBlock slide)
    {
        var number = string.Format(CultureInfo.CurrentCulture, Strings.SlideNumber, slide.Number);
        var heading = Heading([new TextRun(slide.Runs.IsEmpty ? number : number + ": "), .. slide.Runs], level: 2, BlockAlignment.Left);
        if (_document.Blocks.Count > 0)
        {
            heading.BorderThickness = new Thickness(0, 1, 0, 0);
            heading.Padding = new Thickness(0, Spacing, 0, 0);
            heading.SetResourceReference(Block.BorderBrushProperty, ThemeKeys.Border);
        }

        return heading;
    }

    private static Paragraph Code(string text)
    {
        var code = new Paragraph(new Run(text.TrimEnd('\n'))) { Margin = new Thickness(0, 0, 0, Spacing), Padding = new Thickness(8) };
        code.SetResourceReference(TextElement.FontFamilyProperty, "Font.Code");
        code.SetResourceReference(TextElement.BackgroundProperty, ThemeKeys.MarkdownCodeBlockBackground);
        return code;
    }

    private static Section Quote(IEnumerable<TextRun> runs, string? prefix)
    {
        var paragraph = Paragraph(prefix is null ? runs : [new TextRun(prefix, TextStyle.Bold), .. runs], BlockAlignment.Left, 0);
        var section = new Section(paragraph) { BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(Spacing, 0, 0, 0), Margin = new Thickness(0, 0, 0, Spacing) };
        section.SetResourceReference(Block.BorderBrushProperty, ThemeKeys.MarkdownQuoteBorder);
        section.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.TextSecondary);
        return section;
    }

    private static Paragraph Rule()
    {
        var rule = new Paragraph { BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 0, 0, Spacing), FontSize = 1, LineHeight = 1 };
        rule.SetResourceReference(Block.BorderBrushProperty, ThemeKeys.Border);
        return rule;
    }

    private void AddListItem(DocumentBlock block)
    {
        var level = Math.Max(block.Level, 0);
        while (_lists.Count > level + 1 || (_lists.Count == level + 1 && _lists[level].Ordered != block.IsOrdered))
        {
            _lists.RemoveAt(_lists.Count - 1);
        }

        while (_lists.Count < level + 1)
        {
            OpenList(block);
        }

        var item = new ListItem(Paragraph(block.Runs, BlockAlignment.Left, TightSpacing));
        _lists[level].List.ListItems.Add(item);
    }

    // A top-level list goes into the document; a nested one into the last item of the list one level up.
    private void OpenList(DocumentBlock block)
    {
        var list = new WpfList
        {
            MarkerStyle = block.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(0, 0, 0, _lists.Count == 0 ? Spacing : 0),
            Padding = new Thickness(ListIndent, 0, 0, 0),
        };
        if (block.IsOrdered)
        {
            list.StartIndex = Math.Max(block.Number, 1);
        }

        if (_lists.Count == 0)
        {
            _document.Blocks.Add(list);
        }
        else
        {
            var parent = _lists[^1].List;
            if (parent.ListItems.Count == 0)
            {
                parent.ListItems.Add(new ListItem());
            }

            parent.ListItems.LastListItem.Blocks.Add(list);
        }

        _lists.Add((list, block.IsOrdered));
    }

    private static WpfTable Table(DocumentTable source, int rowCount)
    {
        var table = new WpfTable { CellSpacing = 0, BorderThickness = new Thickness(1, 1, 0, 0), Margin = new Thickness(0, 0, 0, Spacing) };
        table.SetResourceReference(Block.BorderBrushProperty, ThemeKeys.Border);
        var group = new TableRowGroup();
        var columns = source.ColumnCount;
        for (var index = 0; index < rowCount; index++)
        {
            var row = new TableRow();
            if (index == 0)
            {
                row.FontWeight = FontWeights.SemiBold;
                row.SetResourceReference(TextElement.BackgroundProperty, ThemeKeys.WidgetBackground);
            }

            for (var column = 0; column < columns; column++)
            {
                row.Cells.Add(Cell(column < source.Rows[index].Length ? source.Rows[index][column] : []));
            }

            group.Rows.Add(row);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private static TableCell Cell(IEnumerable<TextRun> runs)
    {
        var cell = new TableCell(Paragraph(runs, BlockAlignment.Left, 0))
        {
            Padding = new Thickness(CellPadding, TightSpacing, CellPadding, TightSpacing),
            BorderThickness = new Thickness(0, 0, 1, 1),
        };
        cell.SetResourceReference(TableCell.BorderBrushProperty, ThemeKeys.Border);
        return cell;
    }

    private static TextAlignment Alignment(BlockAlignment alignment) => alignment switch
    {
        BlockAlignment.Center => TextAlignment.Center,
        BlockAlignment.Right => TextAlignment.Right,
        BlockAlignment.Justify => TextAlignment.Justify,
        _ => TextAlignment.Left,
    };
}
