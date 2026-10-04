using System.Collections.Immutable;
using System.Globalization;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DrawingProperties = DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties;
using OfficeMath = DocumentFormat.OpenXml.Math.OfficeMath;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>
/// Styled text of a Word paragraph: runs, hyperlinks, fields, tracked changes. A field code (<c>PAGE</c>,
/// <c>HYPERLINK …</c>) is not text; only its result shows. Tracked deletions are skipped, insertions are read. An image
/// becomes a placeholder with its caption.
/// </summary>
internal sealed class WordRuns(MainDocumentPart main, WordStyleSheet styles)
{
    private readonly Dictionary<string, string> _links = main.HyperlinkRelationships.ToDictionary(static link => link.Id, static link => link.Uri.ToString(), StringComparer.Ordinal);

    // Open fields: false while in the field code, true in its result.
    private readonly Stack<bool> _fields = new();
    private readonly RunsBuilder _builder = new();

    public ImmutableArray<TextRun> Read(OpenXmlElement container)
    {
        _fields.Clear();
        Walk(container, link: null);
        return _builder.Build();
    }

    private void Walk(OpenXmlElement element, string? link)
    {
        switch (element)
        {
            case Run run:
                AppendRun(run, link);
                break;
            case Hyperlink hyperlink:
                var url = hyperlink.Id?.Value is { } id && _links.TryGetValue(id, out var address) ? address : null;
                WalkChildren(hyperlink, url ?? link);
                break;
            case OfficeMath math:
                Append(math.InnerText, TextStyle.None, link);
                break;
            case DeletedRun or MoveFromRun or ParagraphProperties:
                break;
            default:
                WalkChildren(element, link);
                break;
        }
    }

    private void WalkChildren(OpenXmlElement element, string? link)
    {
        foreach (var child in element.ChildElements)
        {
            Walk(child, link);
        }
    }

    private void AppendRun(Run run, string? link)
    {
        var style = WordFormatting.Style(run.RunProperties, styles);
        foreach (var child in run.ChildElements)
        {
            switch (child)
            {
                case Text text:
                    Append(text.Text, style, link);
                    break;
                case TabChar or PositionalTab:
                    Append("\t", style, link);
                    break;
                case Break { Type.Value: var type } when type == BreakValues.Page || type == BreakValues.Column:
                    break;
                case Break or CarriageReturn:
                    Append("\n", style, link);
                    break;
                case NoBreakHyphen:
                    Append("-", style, link);
                    break;
                case FieldChar field:
                    Track(field);
                    break;
                case Drawing drawing:
                    Append(Image(drawing.Descendants<DrawingProperties>().FirstOrDefault()), TextStyle.None, link);
                    break;
                default:
                    break;
            }
        }
    }

    private void Append(string text, TextStyle style, string? link)
    {
        // Text inside a field code is hidden: only the field result shows.
        if (!_fields.Contains(false))
        {
            _builder.Append(text, style, link);
        }
    }

    private void Track(FieldChar field)
    {
        var type = field.FieldCharType?.Value;
        if (type == FieldCharValues.Begin)
        {
            _fields.Push(false);
        }
        else if (type == FieldCharValues.Separate && _fields.Count > 0)
        {
            _fields.Pop();
            _fields.Push(true);
        }
        else if (type == FieldCharValues.End && _fields.Count > 0)
        {
            _fields.Pop();
        }
    }

    private static string Image(DrawingProperties? properties) =>
        properties?.Description?.Value is { Length: > 0 } description ? string.Format(CultureInfo.CurrentCulture, Strings.ImagePlaceholder, description)
        : properties?.Title?.Value is { Length: > 0 } title ? string.Format(CultureInfo.CurrentCulture, Strings.ImagePlaceholder, title)
        : Strings.ImageWithoutName;
}
