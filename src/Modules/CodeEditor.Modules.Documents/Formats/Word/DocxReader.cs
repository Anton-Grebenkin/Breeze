using System.Collections.Immutable;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>
/// Word document (.docx) to blocks: headings by style, list items with levels and numbers, tables, code, quotes,
/// aligned paragraphs. Headers, footers, footnotes and comments are not read. O(n) in the document size.
/// </summary>
internal sealed class DocxReader
{
    private readonly WordStyleSheet _styles;
    private readonly WordNumbering _numbering;
    private readonly WordRuns _runs;
    private readonly ImmutableArray<DocumentBlock>.Builder _blocks = ImmutableArray.CreateBuilder<DocumentBlock>();

    private DocxReader(MainDocumentPart main)
    {
        _styles = WordStyleSheet.Load(main);
        _numbering = WordNumbering.Load(main);
        _runs = new WordRuns(main, _styles);
    }

    /// <exception cref="InvalidDataException">The file is not a Word document.</exception>
    public static RichDocument Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var document = OpenXmlFiles.OpenWord(stream, editable: false);
        return Read(document);
    }

    public static RichDocument Read(WordprocessingDocument document)
    {
        var main = document.MainDocumentPart ?? throw OpenXmlFiles.NotDocument();
        var reader = new DocxReader(main);
        reader.AddBlocks(main.Document?.Body?.ChildElements ?? []);
        return new RichDocument(reader._blocks.ToImmutable());
    }

    private void AddBlocks(IEnumerable<OpenXmlElement> elements)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case Paragraph paragraph when Convert(paragraph) is { } block:
                    _blocks.Add(block);
                    break;
                case Table table:
                    _blocks.Add(new DocumentBlock(DocumentBlockKind.Table, []) { Table = ReadTable(table) });
                    break;
                case SdtBlock content:
                    AddBlocks(content.SdtContentBlock?.ChildElements ?? []);
                    break;
                case CustomXmlBlock custom:
                    AddBlocks(custom.ChildElements);
                    break;
                default:
                    break;
            }
        }
    }

    private DocumentBlock? Convert(Paragraph paragraph)
    {
        var runs = _runs.Read(paragraph);
        if (runs.All(run => string.IsNullOrWhiteSpace(run.Text)))
        {
            return null;
        }

        var properties = paragraph.ParagraphProperties;
        var styleId = properties?.ParagraphStyleId?.Val?.Value;
        var alignment = WordFormatting.Alignment(properties);
        var heading = properties?.OutlineLevel?.Val?.Value is { } outline and < 6 ? outline + 1 : _styles.HeadingLevel(styleId);
        if (heading > 0)
        {
            return new DocumentBlock(DocumentBlockKind.Heading, Trimmed(runs)) { Level = heading, Alignment = alignment };
        }

        if (ListItem(properties?.NumberingProperties, _styles.Numbering(styleId), runs) is { } item)
        {
            return item;
        }

        if (_styles.IsCode(styleId) || runs.All(run => run.Has(TextStyle.Code) || string.IsNullOrWhiteSpace(run.Text)))
        {
            return new DocumentBlock(DocumentBlockKind.Code, [new TextRun(string.Concat(runs.Select(run => run.Text)))]);
        }

        var kind = _styles.IsQuote(styleId) ? DocumentBlockKind.Quote : DocumentBlockKind.Paragraph;
        return new DocumentBlock(kind, runs) { Alignment = alignment };
    }

    // List and level come from the paragraph, missing ones from its style: a "List Bullet 2" paragraph sets only the
    // level.
    private DocumentBlock? ListItem(NumberingProperties? own, NumberingProperties? style, ImmutableArray<TextRun> runs)
    {
        var numberId = own?.NumberingId?.Val?.Value ?? style?.NumberingId?.Val?.Value ?? 0;
        var level = own?.NumberingLevelReference?.Val?.Value ?? style?.NumberingLevelReference?.Val?.Value ?? 0;
        return numberId > 0 && _numbering.Next(numberId, level) is { } item
            ? new DocumentBlock(DocumentBlockKind.ListItem, Trimmed(runs)) { Level = level, IsOrdered = item.IsOrdered, Number = item.Number }
            : null;
    }

    private DocumentTable ReadTable(Table table)
    {
        var rows = ImmutableArray.CreateBuilder<ImmutableArray<ImmutableArray<TextRun>>>();
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = ImmutableArray.CreateBuilder<ImmutableArray<TextRun>>();
            foreach (var cell in row.Elements<TableCell>())
            {
                cells.Add(CellRuns(cell));

                // A horizontally merged cell spans several columns: the extra ones are empty.
                var span = cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1;
                for (var extra = 1; extra < span; extra++)
                {
                    cells.Add([]);
                }
            }

            rows.Add(cells.ToImmutable());
        }

        return new DocumentTable(rows.ToImmutable());
    }

    // Cell paragraphs (nested tables included) joined with line breaks.
    private ImmutableArray<TextRun> CellRuns(TableCell cell)
    {
        var runs = ImmutableArray.CreateBuilder<TextRun>();
        foreach (var paragraph in cell.Descendants<Paragraph>())
        {
            var text = _runs.Read(paragraph);
            if (text.IsEmpty)
            {
                continue;
            }

            if (runs.Count > 0)
            {
                runs.Add(new TextRun("\n"));
            }

            runs.AddRange(text);
        }

        return runs.ToImmutable();
    }

    // Edge spaces of headings and items come from tabs and indents, not meaning.
    private static ImmutableArray<TextRun> Trimmed(ImmutableArray<TextRun> runs) =>
        runs.Length == 1 ? [runs[0] with { Text = runs[0].Text.Trim() }] : runs;
}
