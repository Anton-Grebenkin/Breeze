using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>
/// Styles used to write blocks: normal text, headings 1–6, list paragraph, code, quote, hyperlink, table grid. A new
/// document gets them created; an existing one reuses its styles with the same built-in names ("heading 1") and gets
/// the missing ones based on its normal style, so inserted content looks like the rest of the document.
/// </summary>
internal static class WordStyles
{
    public const string BodyFont = "Calibri";
    public const string CodeFont = "Consolas";

    private const string NormalName = "Normal";
    private const int HeadingLevels = 6;
    private static readonly int[] HeadingSizes = [32, 28, 26, 24, 22, 22];

    /// <summary>Styles of a new document: default font and spacing, the normal style and all block styles.</summary>
    public static Styles Create()
    {
        var styles = new Styles(new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = BodyFont, HighAnsi = BodyFont, ComplexScript = BodyFont, EastAsia = BodyFont },
                new FontSize { Val = "22" },
                new FontSizeComplexScript { Val = "22" })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new SpacingBetweenLines { After = "120", Line = "264", LineRule = LineSpacingRuleValues.Auto }))));
        styles.Append(new Style(new StyleName { Val = NormalName }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = NormalName, Default = true });
        foreach (var (name, id, create) in Definitions(NormalName))
        {
            styles.Append(create(id, name));
        }

        return styles;
    }

    /// <summary>Block style ids in the document; missing styles are added to its styles part.</summary>
    public static WordStyleIds Ensure(MainDocumentPart main)
    {
        var part = main.StyleDefinitionsPart ?? main.AddNewPart<StyleDefinitionsPart>();
        part.Styles ??= Create();
        var sheet = WordStyleSheet.Load(main);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, id, create) in Definitions(sheet.IdByName(NormalName) ?? NormalName))
        {
            if (sheet.IdByName(name) is { } existing)
            {
                ids[name] = existing;
                continue;
            }

            // The id is taken by a style with another name (Russian Word uses digits and letters as ids): use a
            // prefixed id.
            var free = sheet.Contains(id) ? "CodeEditor" + id : id;
            part.Styles.Append(create(free, name));
            ids[name] = free;
        }

        return new WordStyleIds(
            [.. Enumerable.Range(1, HeadingLevels).Select(level => ids[HeadingName(level)])],
            ids["List Paragraph"], ids["Code"], ids["Quote"], ids["Hyperlink"], ids["Table Grid"]);
    }

    private static string HeadingName(int level) => "heading " + level.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<(string Name, string Id, Func<string, string, Style> Create)> Definitions(string normal)
    {
        for (var level = 1; level <= HeadingLevels; level++)
        {
            var current = level;
            yield return (HeadingName(level), "Heading" + level.ToString(CultureInfo.InvariantCulture), (id, name) => Heading(id, name, normal, current));
        }

        yield return ("List Paragraph", "ListParagraph", (id, name) => Paragraph(id, name, normal, new StyleParagraphProperties(new Indentation { Left = "720" })));
        yield return ("Code", "Code", (id, name) => Paragraph(id, name, normal,
            new StyleParagraphProperties(
                new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "F2F2F2" },
                new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }),
            new StyleRunProperties(new RunFonts { Ascii = CodeFont, HighAnsi = CodeFont, ComplexScript = CodeFont }, new FontSize { Val = "20" })));
        yield return ("Quote", "Quote", (id, name) => Paragraph(id, name, normal,
            new StyleParagraphProperties(new Indentation { Left = "720" }),
            new StyleRunProperties(new Italic(), new Color { Val = "595959" })));
        yield return ("Hyperlink", "Hyperlink", (id, name) => new Style(new StyleName { Val = name }, new UIPriority { Val = 99 },
            new StyleRunProperties(new Color { Val = "0563C1" }, new Underline { Val = UnderlineValues.Single })) { Type = StyleValues.Character, StyleId = id });
        yield return ("Table Grid", "TableGrid", (id, name) => TableGrid(id, name));
    }

    private static Style Heading(string id, string name, string normal, int level) => new(
        new StyleName { Val = name },
        new BasedOn { Val = normal },
        new NextParagraphStyle { Val = normal },
        new UIPriority { Val = 9 },
        new PrimaryStyle(),
        new StyleParagraphProperties(
            new KeepNext(),
            new KeepLines(),
            new SpacingBetweenLines { Before = level == 1 ? "360" : "240", After = "120" },
            new OutlineLevel { Val = level - 1 }),
        new StyleRunProperties(new Bold(), new FontSize { Val = HeadingSizes[level - 1].ToString(CultureInfo.InvariantCulture) }))
    {
        Type = StyleValues.Paragraph,
        StyleId = id,
    };

    private static Style Paragraph(string id, string name, string normal, params OpenXmlElement[] properties)
    {
        var style = new Style(new StyleName { Val = name }, new BasedOn { Val = normal }, new UIPriority { Val = 34 }) { Type = StyleValues.Paragraph, StyleId = id };
        style.Append(properties);
        return style;
    }

    private static Style TableGrid(string id, string name) => new(
        new StyleName { Val = name },
        new UIPriority { Val = 39 },
        new StyleTableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "auto" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "auto" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "auto" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "auto" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "auto" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "auto" }),
            new TableCellMarginDefault(
                new TopMargin { Width = "28", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "28", Type = TableWidthUnitValues.Dxa })))
    {
        Type = StyleValues.Table,
        StyleId = id,
    };
}
