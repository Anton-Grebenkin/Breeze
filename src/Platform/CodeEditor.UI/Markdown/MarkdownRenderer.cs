using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using CodeEditor.UI.Themes;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using WpfBlock = System.Windows.Documents.Block;
using WpfList = System.Windows.Documents.List;

namespace CodeEditor.UI.Markdown;

/// <summary>
/// Markdown → <see cref="FlowDocument"/> blocks: headings, paragraphs, lists and tasks, quotes, tables, code blocks.
/// Colors and fonts are theme resource references, so a theme switch recolors shown text by itself; code highlighting
/// is redrawn by <see cref="MarkdownViewer"/> on the colorizer's event.
/// </summary>
/// <remarks>O(n) in text length: Markdig parses in one pass and the tree is visited once per node.</remarks>
public sealed class MarkdownRenderer(ICodeColorizer? colorizer)
{
    private const double Spacing = 10;
    private const double TightSpacing = 2;
    private const double ListIndent = 20;
    private const double CellPadding = 6;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
        .Build();

    /// <summary>
    /// Top-level blocks with their source text, which <see cref="MarkdownViewer"/> uses to detect unchanged blocks and
    /// rebuild only the tail while streaming. O(n) in text length.
    /// </summary>
    public IReadOnlyList<MarkdownSourceBlock> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var blocks = new List<MarkdownSourceBlock>();
        foreach (var block in Markdig.Markdown.Parse(markdown, Pipeline))
        {
            if (block is not LinkReferenceDefinitionGroup)
            {
                blocks.Add(new MarkdownSourceBlock(SourceOf(markdown, block), block));
            }
        }

        return blocks;
    }

    /// <summary>A document block; unknown blocks become empty sections so blocks and sources stay one-to-one.</summary>
    public WpfBlock Convert(MarkdownSourceBlock block) => Convert(block.Block) ?? new Section();

    private static string SourceOf(string markdown, MdBlock block) =>
        block.Span.Start >= 0 && block.Span.End < markdown.Length && !block.Span.IsEmpty
            ? markdown.Substring(block.Span.Start, block.Span.Length)
            : string.Empty;

    private void AddBlocks(BlockCollection target, ContainerBlock source)
    {
        foreach (var block in source)
        {
            if (Convert(block) is { } converted)
            {
                target.Add(converted);
            }
        }
    }

    // Derived types must be matched before their bases: FencedCodeBlock is a CodeBlock.
    private WpfBlock? Convert(MdBlock block) => block switch
    {
        HeadingBlock heading => CreateHeading(heading),
        ParagraphBlock paragraph => CreateParagraph(paragraph.Inline, Spacing),
        ListBlock list => CreateList(list),
        QuoteBlock quote => CreateQuote(quote),
        MdTable table => CreateTable(table),
        FencedCodeBlock fenced => MarkdownCodeBlock.Create(fenced.Lines.ToString(), fenced.Info, colorizer),
        CodeBlock code => MarkdownCodeBlock.Create(code.Lines.ToString(), null, colorizer),
        HtmlBlock html => new Paragraph(new Run(html.Lines.ToString())) { Margin = new Thickness(0, 0, 0, Spacing) },
        ThematicBreakBlock => CreateRule(),
        LinkReferenceDefinitionGroup => null,
        ContainerBlock container => CreateSection(container),
        _ => null,
    };

    private static Paragraph CreateParagraph(ContainerInline? inline, double bottom)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, bottom) };
        MarkdownInlines.AddTo(paragraph.Inlines, inline);
        return paragraph;
    }

    private static Paragraph CreateHeading(HeadingBlock heading)
    {
        var paragraph = CreateParagraph(heading.Inline, Spacing / 2);
        paragraph.Margin = paragraph.Margin with { Top = Spacing };
        paragraph.FontWeight = FontWeights.SemiBold;
        paragraph.SetResourceReference(TextElement.FontSizeProperty, heading.Level switch
        {
            1 => "FontSize.Markdown.H1",
            2 => "FontSize.Markdown.H2",
            _ => "FontSize.Markdown.H3",
        });
        if (heading.Level == 1)
        {
            paragraph.SetResourceReference(TextElement.FontFamilyProperty, "Font.Display");
        }

        return paragraph;
    }

    private WpfList CreateList(ListBlock source)
    {
        var list = new WpfList
        {
            MarkerStyle = source.IsOrdered ? TextMarkerStyle.Decimal : IsTaskList(source) ? TextMarkerStyle.None : TextMarkerStyle.Disc,
            Margin = new Thickness(0, 0, 0, Spacing),
            Padding = new Thickness(ListIndent, 0, 0, 0),
        };
        if (source.IsOrdered && int.TryParse(source.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var start))
        {
            list.StartIndex = start;
        }

        foreach (var item in source.OfType<ListItemBlock>())
        {
            var listItem = new ListItem();
            AddBlocks(listItem.Blocks, item);
            foreach (var block in listItem.Blocks)
            {
                block.Margin = block.Margin with { Bottom = source.IsLoose ? Spacing : TightSpacing };
            }

            list.ListItems.Add(listItem);
        }

        return list;
    }

    private static bool IsTaskList(ListBlock list) =>
        list.FirstOrDefault() is ListItemBlock { Count: > 0 } item && item[0] is ParagraphBlock { Inline.FirstChild: TaskList };

    private Section CreateQuote(QuoteBlock quote)
    {
        var section = CreateSection(quote);
        section.BorderThickness = new Thickness(3, 0, 0, 0);
        section.Padding = new Thickness(Spacing, 0, 0, 0);
        section.Margin = new Thickness(0, 0, 0, Spacing);
        section.SetResourceReference(WpfBlock.BorderBrushProperty, ThemeKeys.MarkdownQuoteBorder);
        section.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.TextSecondary);
        return section;
    }

    private Section CreateSection(ContainerBlock container)
    {
        var section = new Section();
        AddBlocks(section.Blocks, container);
        return section;
    }

    private Table CreateTable(MdTable source)
    {
        var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(1, 1, 0, 0), Margin = new Thickness(0, 0, 0, Spacing) };
        table.SetResourceReference(WpfBlock.BorderBrushProperty, ThemeKeys.Border);
        var group = new TableRowGroup();
        foreach (var sourceRow in source.OfType<MdTableRow>())
        {
            var row = new TableRow();
            if (sourceRow.IsHeader)
            {
                row.FontWeight = FontWeights.SemiBold;
                row.SetResourceReference(TextElement.BackgroundProperty, ThemeKeys.WidgetBackground);
            }

            foreach (var sourceCell in sourceRow.OfType<MdTableCell>())
            {
                row.Cells.Add(CreateCell(sourceCell));
            }

            group.Rows.Add(row);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private TableCell CreateCell(MdTableCell source)
    {
        var cell = new TableCell
        {
            Padding = new Thickness(CellPadding, TightSpacing, CellPadding, TightSpacing),
            BorderThickness = new Thickness(0, 0, 1, 1),
        };
        cell.SetResourceReference(TableCell.BorderBrushProperty, ThemeKeys.Border);
        AddBlocks(cell.Blocks, source);
        foreach (var block in cell.Blocks)
        {
            block.Margin = new Thickness(0);
        }

        return cell;
    }

    private static Paragraph CreateRule()
    {
        var rule = new Paragraph { BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 0, 0, Spacing), FontSize = 1, LineHeight = 1 };
        rule.SetResourceReference(WpfBlock.BorderBrushProperty, ThemeKeys.Border);
        return rule;
    }
}
