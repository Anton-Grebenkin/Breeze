using System.Collections.Immutable;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace CodeEditor.Modules.Documents.Formats.Slides;

/// <summary>
/// Slide shape text: DrawingML paragraphs with styles and links. A bullet is explicit (<c>buChar</c>,
/// <c>buAutoNum</c>) or comes from a body placeholder, which has bullets by default; <c>buNone</c> removes them.
/// </summary>
internal sealed class PptxText(OpenXmlPart part)
{
    private readonly Dictionary<string, string> _links = part.HyperlinkRelationships.ToDictionary(static link => link.Id, static link => link.Uri.ToString(), StringComparer.Ordinal);

    /// <summary>
    /// The shape's placeholder type; a placeholder without a type is an object (bulleted text); <c>null</c> if the shape
    /// is not a placeholder.
    /// </summary>
    public static PlaceholderValues? Placeholder(Shape shape) =>
        shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape is { } placeholder
            ? placeholder.Type?.Value ?? PlaceholderValues.Object
            : null;

    public static bool IsTitle(PlaceholderValues? type) => type is { } value && (value == PlaceholderValues.Title || value == PlaceholderValues.CenteredTitle);

    /// <summary>Slide number, date, header and footer come from the layout and are not content.</summary>
    public static bool IsBoilerplate(PlaceholderValues? type) => type is { } value
        && (value == PlaceholderValues.SlideNumber || value == PlaceholderValues.DateAndTime || value == PlaceholderValues.Footer || value == PlaceholderValues.Header);

    /// <summary>The slide title as one line.</summary>
    public ImmutableArray<TextRun> Title(Shape shape)
    {
        var builder = new RunsBuilder();
        foreach (var paragraph in shape.TextBody?.Elements<A.Paragraph>() ?? [])
        {
            if (builder.HasText)
            {
                builder.Append(" ");
            }

            AppendRuns(builder, paragraph);
        }

        return builder.Build();
    }

    /// <summary>Shape paragraphs: bulleted ones become list items with a level, the rest plain paragraphs.</summary>
    public IEnumerable<DocumentBlock> Paragraphs(Shape shape)
    {
        var bulletsByDefault = Placeholder(shape) is { } type && (type == PlaceholderValues.Body || type == PlaceholderValues.Object);
        var counters = new int[9];
        foreach (var paragraph in shape.TextBody?.Elements<A.Paragraph>() ?? [])
        {
            var builder = new RunsBuilder();
            AppendRuns(builder, paragraph);
            if (!builder.HasText)
            {
                continue;
            }

            var properties = paragraph.ParagraphProperties;
            var level = Math.Clamp(properties?.Level?.Value ?? 0, 0, counters.Length - 1);
            var ordered = properties?.GetFirstChild<A.AutoNumberedBullet>() is not null;
            var bullet = ordered || properties?.GetFirstChild<A.CharacterBullet>() is not null
                || (bulletsByDefault && properties?.GetFirstChild<A.NoBullet>() is null);
            if (!bullet)
            {
                yield return new DocumentBlock(DocumentBlockKind.Paragraph, builder.Build());
                continue;
            }

            counters[level]++;
            Array.Clear(counters, level + 1, counters.Length - level - 1);
            yield return new DocumentBlock(DocumentBlockKind.ListItem, builder.Build()) { Level = level, IsOrdered = ordered, Number = counters[level] };
        }
    }

    public DocumentTable Table(A.Table table) => new(
    [
        .. table.Elements<A.TableRow>().Select(row => row.Elements<A.TableCell>().Select(CellRuns).ToImmutableArray()),
    ]);

    private ImmutableArray<TextRun> CellRuns(A.TableCell cell)
    {
        var builder = new RunsBuilder();
        foreach (var paragraph in cell.TextBody?.Elements<A.Paragraph>() ?? [])
        {
            if (builder.HasText)
            {
                builder.Append("\n");
            }

            AppendRuns(builder, paragraph);
        }

        return builder.Build();
    }

    private void AppendRuns(RunsBuilder builder, A.Paragraph paragraph)
    {
        foreach (var child in paragraph.ChildElements)
        {
            switch (child)
            {
                case A.Run run:
                    builder.Append(run.Text?.Text ?? string.Empty, Style(run.RunProperties), Link(run.RunProperties));
                    break;
                case A.Field field:
                    builder.Append(field.Text?.Text ?? string.Empty, Style(field.RunProperties), Link(field.RunProperties));
                    break;
                case A.Break:
                    builder.Append("\n");
                    break;
                default:
                    break;
            }
        }
    }

    private static TextStyle Style(A.TextCharacterPropertiesType? properties)
    {
        if (properties is null)
        {
            return TextStyle.None;
        }

        var style = TextStyle.None;
        style |= properties.Bold?.Value == true ? TextStyle.Bold : TextStyle.None;
        style |= properties.Italic?.Value == true ? TextStyle.Italic : TextStyle.None;
        style |= properties.Underline?.Value is { } underline && underline != A.TextUnderlineValues.None ? TextStyle.Underline : TextStyle.None;
        style |= properties.Strike?.Value is { } strike && strike != A.TextStrikeValues.NoStrike ? TextStyle.Strike : TextStyle.None;
        return style;
    }

    private string? Link(A.TextCharacterPropertiesType? properties) =>
        properties?.GetFirstChild<A.HyperlinkOnClick>()?.Id?.Value is { } id && _links.TryGetValue(id, out var url) ? url : null;
}
