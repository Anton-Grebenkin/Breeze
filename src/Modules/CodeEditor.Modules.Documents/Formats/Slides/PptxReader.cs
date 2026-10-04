using System.Collections.Immutable;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Table = DocumentFormat.OpenXml.Drawing.Table;

namespace CodeEditor.Modules.Documents.Formats.Slides;

/// <summary>
/// PowerPoint presentation (.pptx) to blocks: for each slide its number and title, shape text with bullet levels,
/// tables and speaker notes. Images, charts and formatting are not read. Layout headers, footers, date and slide
/// number are skipped.
/// </summary>
internal sealed class PptxReader
{
    private readonly ImmutableArray<DocumentBlock>.Builder _blocks = ImmutableArray.CreateBuilder<DocumentBlock>();

    /// <exception cref="InvalidDataException">The file is not a presentation.</exception>
    public static RichDocument Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var document = OpenXmlFiles.OpenPowerPoint(stream, editable: false);
        var presentation = document.PresentationPart ?? throw OpenXmlFiles.NotDocument();
        var reader = new PptxReader();
        var number = 0;
        foreach (var slideId in presentation.Presentation?.SlideIdList?.Elements<SlideId>() ?? [])
        {
            number++;
            if (slideId.RelationshipId?.Value is { } id && presentation.TryGetPartById(id, out var part) && part is SlidePart slide)
            {
                reader.AddSlide(slide, number);
            }
        }

        return new RichDocument(reader._blocks.ToImmutable());
    }

    private void AddSlide(SlidePart slide, int number)
    {
        var text = new PptxText(slide);
        var shapes = slide.Slide?.CommonSlideData?.ShapeTree?.ChildElements ?? (IEnumerable<OpenXmlElement>)[];
        var all = Flatten(shapes).ToList();
        var title = all.OfType<Shape>().FirstOrDefault(shape => PptxText.IsTitle(PptxText.Placeholder(shape)));
        _blocks.Add(new DocumentBlock(DocumentBlockKind.Slide, title is null ? [] : text.Title(title)) { Number = number });
        foreach (var element in all)
        {
            switch (element)
            {
                case Shape shape when !ReferenceEquals(shape, title) && !PptxText.IsBoilerplate(PptxText.Placeholder(shape)):
                    _blocks.AddRange(text.Paragraphs(shape));
                    break;
                case GraphicFrame frame when frame.Descendants<Table>().FirstOrDefault() is { } table:
                    _blocks.Add(new DocumentBlock(DocumentBlockKind.Table, []) { Table = text.Table(table) });
                    break;
                default:
                    break;
            }
        }

        AddNotes(slide);
    }

    private void AddNotes(SlidePart slide)
    {
        if (slide.NotesSlidePart?.NotesSlide?.CommonSlideData?.ShapeTree is not { } tree)
        {
            return;
        }

        var text = new PptxText(slide.NotesSlidePart);
        var notes = tree.Descendants<Shape>()
            .Where(shape => PptxText.Placeholder(shape) is { } type && type == PlaceholderValues.Body)
            .SelectMany(text.Paragraphs)
            .Select(block => block.PlainText)
            .Where(line => line.Length > 0)
            .ToList();
        if (notes.Count > 0)
        {
            _blocks.Add(DocumentBlock.Of(DocumentBlockKind.Notes, string.Join('\n', notes)));
        }
    }

    // Grouped shapes are inlined in order: the shape tree order is the slide's reading order.
    private static IEnumerable<OpenXmlElement> Flatten(IEnumerable<OpenXmlElement> elements)
    {
        foreach (var element in elements)
        {
            if (element is GroupShape group)
            {
                foreach (var child in Flatten(group.ChildElements))
                {
                    yield return child;
                }

                continue;
            }

            yield return element;
        }
    }
}
