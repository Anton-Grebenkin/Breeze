using System.Collections.Immutable;
using System.Globalization;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;

namespace CodeEditor.Modules.Documents.Formats;

/// <summary>
/// Model Markdown to document blocks, from which Word and PDF are written. Headings, paragraphs, nested numbered lists,
/// tables, code, quotes, rules; **bold**, *italic*, ~~strikethrough~~, `code`, links. Images become a caption: the model
/// has no image files. O(n) in the text length.
/// </summary>
internal sealed class MarkdownReader
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
        .Build();

    private readonly ImmutableArray<DocumentBlock>.Builder _blocks = ImmutableArray.CreateBuilder<DocumentBlock>();

    public static RichDocument Read(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var reader = new MarkdownReader();
        reader.AddBlocks(Markdown.Parse(markdown, Pipeline), listLevel: -1, quoted: false);
        return new RichDocument(reader._blocks.ToImmutable());
    }

    private void AddBlocks(ContainerBlock container, int listLevel, bool quoted)
    {
        foreach (var block in container)
        {
            AddBlock(block, listLevel, quoted);
        }
    }

    // Derived types come before base types: FencedCodeBlock is a CodeBlock, ListBlock is a ContainerBlock.
    private void AddBlock(Block block, int listLevel, bool quoted)
    {
        switch (block)
        {
            case HeadingBlock heading:
                _blocks.Add(new DocumentBlock(DocumentBlockKind.Heading, Inlines(heading.Inline)) { Level = heading.Level });
                break;
            case ParagraphBlock paragraph:
                _blocks.Add(new DocumentBlock(quoted ? DocumentBlockKind.Quote : DocumentBlockKind.Paragraph, Inlines(paragraph.Inline)));
                break;
            case ListBlock list:
                AddList(list, listLevel + 1, quoted);
                break;
            case QuoteBlock quote:
                AddBlocks(quote, listLevel, quoted: true);
                break;
            case MdTable table:
                _blocks.Add(new DocumentBlock(DocumentBlockKind.Table, []) { Table = Table(table) });
                break;
            case CodeBlock code:
                _blocks.Add(DocumentBlock.Of(DocumentBlockKind.Code, code.Lines.ToString()));
                break;
            case ThematicBreakBlock:
                _blocks.Add(new DocumentBlock(DocumentBlockKind.Rule, []));
                break;
            case HtmlBlock html:
                _blocks.Add(DocumentBlock.Of(DocumentBlockKind.Paragraph, html.Lines.ToString()));
                break;
            case ContainerBlock container and not LinkReferenceDefinitionGroup:
                AddBlocks(container, listLevel, quoted);
                break;
            default:
                break;
        }
    }

    // An item's first paragraph is the item itself, a nested list goes one level deeper, the rest follows as blocks.
    private void AddList(ListBlock list, int level, bool quoted)
    {
        var number = list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var start) ? start : 1;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var added = false;
            foreach (var child in item)
            {
                if (!added && child is ParagraphBlock paragraph)
                {
                    _blocks.Add(Item(Inlines(paragraph.Inline), level, list.IsOrdered, number));
                    added = true;
                }
                else
                {
                    AddBlock(child, level, quoted);
                }
            }

            if (!added)
            {
                _blocks.Add(Item([], level, list.IsOrdered, number));
            }

            number++;
        }
    }

    private static DocumentBlock Item(ImmutableArray<TextRun> runs, int level, bool ordered, int number) =>
        new(DocumentBlockKind.ListItem, runs) { Level = level, IsOrdered = ordered, Number = number };

    private static DocumentTable Table(MdTable table) => new(
    [
        .. table.OfType<MdTableRow>().Select(row => row.OfType<MdTableCell>().Select(CellRuns).ToImmutableArray()),
    ]);

    private static ImmutableArray<TextRun> CellRuns(MdTableCell cell)
    {
        var builder = new RunsBuilder();
        foreach (var paragraph in cell.OfType<ParagraphBlock>())
        {
            if (builder.HasText)
            {
                builder.Append("\n");
            }

            AddInlines(builder, paragraph.Inline, TextStyle.None, link: null);
        }

        return builder.Build();
    }

    private static ImmutableArray<TextRun> Inlines(ContainerInline? container)
    {
        var builder = new RunsBuilder();
        AddInlines(builder, container, TextStyle.None, link: null);
        return builder.Build();
    }

    private static void AddInlines(RunsBuilder builder, ContainerInline? container, TextStyle style, string? link)
    {
        foreach (var inline in container ?? (IEnumerable<Inline>)[])
        {
            switch (inline)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString(), style, link);
                    break;
                case EmphasisInline emphasis:
                    AddInlines(builder, emphasis, style | Emphasis(emphasis), link);
                    break;
                case CodeInline code:
                    builder.Append(code.Content, style | TextStyle.Code, link);
                    break;
                case LinkInline { IsImage: true } image:
                    builder.Append(string.Format(CultureInfo.CurrentCulture, Strings.ImagePlaceholder, PlainText(image)), style, link);
                    break;
                case LinkInline anchor:
                    AddInlines(builder, anchor, style, anchor.Url ?? link);
                    break;
                case AutolinkInline auto:
                    builder.Append(auto.Url, style, auto.IsEmail ? "mailto:" + auto.Url : auto.Url);
                    break;
                case LineBreakInline lineBreak:
                    builder.Append(lineBreak.IsHard ? "\n" : " ", style, link);
                    break;
                case HtmlEntityInline entity:
                    builder.Append(entity.Transcoded.ToString(), style, link);
                    break;
                case HtmlInline html when html.Tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase):
                    builder.Append("\n", style, link);
                    break;
                case TaskList task:
                    builder.Append(task.Checked ? "[x] " : "[ ] ", style, link);
                    break;
                case ContainerInline nested:
                    AddInlines(builder, nested, style, link);
                    break;
                default:
                    break;
            }
        }
    }

    private static TextStyle Emphasis(EmphasisInline emphasis) => emphasis.DelimiterChar == '~'
        ? TextStyle.Strike
        : emphasis.DelimiterCount >= 2 ? TextStyle.Bold : TextStyle.Italic;

    private static string PlainText(ContainerInline container) =>
        string.Concat(container.Descendants<LiteralInline>().Select(literal => literal.Content.ToString()));
}
