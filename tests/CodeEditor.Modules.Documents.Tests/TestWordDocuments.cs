using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// Documents as Word saves them: Russian Word (style ids "a", "1", "a3" with English names, a list set by the paragraph
/// style, bold via the "Strong" character style, a content control, bookmarks, merged cells) and numbering with several
/// instances of one list.
/// </summary>
internal static class TestWordDocuments
{
    public static byte[] RussianWord()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(
                Style("a", "Normal", StyleValues.Paragraph),
                Style("1", "heading 1", StyleValues.Paragraph, new StyleParagraphProperties(new OutlineLevel { Val = 0 })),
                Style("a3", "List Bullet", StyleValues.Paragraph, new StyleParagraphProperties(new NumberingProperties(new NumberingId { Val = 1 }))),
                Style("a4", "Strong", StyleValues.Character, new StyleRunProperties(new Bold())));
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new Numbering(
                new AbstractNum(
                    new Level(new NumberingFormat { Val = NumberFormatValues.Bullet }, new LevelText { Val = "•" }) { LevelIndex = 0 },
                    new Level(new NumberingFormat { Val = NumberFormatValues.Bullet }, new LevelText { Val = "o" }) { LevelIndex = 1 }) { AbstractNumberId = 0 },
                new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = 1 });
            main.Document = new Document(new Body(
                Paragraph("1", new Run(new Text("Глава 1"))),
                new Paragraph(
                    new BookmarkStart { Id = "0", Name = "_Toc1" },
                    new Run(new Text("Обычный ") { Space = SpaceProcessingModeValues.Preserve }),
                    new Run(new RunProperties(new RunStyle { Val = "a4" }), new Text("важный")),
                    new Run(new Text(" текст.") { Space = SpaceProcessingModeValues.Preserve }),
                    new BookmarkEnd { Id = "0" }),
                new SdtBlock(new SdtProperties(new SdtAlias { Val = "Поле" }), new SdtContentBlock(new Paragraph(new Run(new Text("В элементе управления"))))),
                Paragraph("a3", new Run(new Text("Пункт"))),
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "a3" }, new NumberingProperties(new NumberingLevelReference { Val = 1 })),
                    new Run(new Text("Подпункт"))),
                new Table(
                    new TableRow(Cell("Шапка", span: 2)),
                    new TableRow(Cell("Слева"), Cell("Справа")),
                    new TableRow(Cell(string.Empty, merged: true), Cell("Ещё")))));
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Numbering as Word leaves it: instances 1 and 2 of one definition continue a shared count, instance 3 with
    /// startOverride restarts it; the part ends with the numIdMacAtCleanup marker from Word for Mac.
    /// </summary>
    public static byte[] SharedNumbering()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new Numbering(
                new AbstractNum(new Level(new StartNumberingValue { Val = 1 }, new NumberingFormat { Val = NumberFormatValues.Decimal }, new LevelText { Val = "%1." }) { LevelIndex = 0 }) { AbstractNumberId = 0 },
                new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = 1 },
                new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = 2 },
                new NumberingInstance(new AbstractNumId { Val = 0 }, new LevelOverride(new StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 }) { NumberID = 3 },
                new NumberingIdMacAtCleanup { Val = 3 });
            main.Document = new Document(new Body(Item(1, "первый"), Item(1, "второй"), Item(2, "третий"), Item(3, "снова первый")));
        }

        return stream.ToArray();
    }

    private static Paragraph Item(int list, string text) =>
        new(new ParagraphProperties(new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = list })), new Run(new Text(text)));

    private static Style Style(string id, string name, StyleValues type, params OpenXmlElement[] properties)
    {
        var style = new Style(new StyleName { Val = name }) { StyleId = id, Type = type };
        style.Append(properties);
        return style;
    }

    private static Paragraph Paragraph(string style, params OpenXmlElement[] content)
    {
        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = style }));
        paragraph.Append(content);
        return paragraph;
    }

    private static TableCell Cell(string text, int span = 1, bool merged = false)
    {
        var properties = new TableCellProperties();
        if (span > 1)
        {
            properties.Append(new GridSpan { Val = span });
        }

        if (merged)
        {
            properties.Append(new VerticalMerge());
        }

        return new TableCell(properties, new Paragraph(new Run(new Text(text))));
    }
}
