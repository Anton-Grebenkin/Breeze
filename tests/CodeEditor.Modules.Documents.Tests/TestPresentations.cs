using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// Test presentation with two slides: a title with bulleted text and notes, a table, and a layout slide number the
/// reader skips. No layouts or themes: the reader doesn't need them.
/// </summary>
internal static class TestPresentations
{
    public static byte[] TwoSlides()
    {
        using var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentation = document.AddPresentationPart();
            var first = AddSlide(presentation,
                Shape(1, PlaceholderValues.Title, Paragraph("Итоги года")),
                Shape(2, placeholder: null, Paragraph("Выручка ", bold: false), Paragraph("Рост на 12 %", bold: true, level: 1)),
                Shape(3, PlaceholderValues.SlideNumber, Paragraph("1")));
            first.AddNewPart<NotesSlidePart>().NotesSlide = new NotesSlide(new CommonSlideData(new ShapeTree(Shape(1, PlaceholderValues.Body, Paragraph("Сказать про филиалы")))));
            var second = AddSlide(presentation, Shape(1, PlaceholderValues.Title, Paragraph("Планы")), Table());
            presentation.Presentation = new Presentation(new SlideIdList(
                new SlideId { Id = 256U, RelationshipId = presentation.GetIdOfPart(first) },
                new SlideId { Id = 257U, RelationshipId = presentation.GetIdOfPart(second) }));
        }

        return stream.ToArray();
    }

    private static SlidePart AddSlide(PresentationPart presentation, params OpenXmlElement[] shapes)
    {
        var slide = presentation.AddNewPart<SlidePart>();
        slide.Slide = new Slide(new CommonSlideData(new ShapeTree(shapes)));
        return slide;
    }

    // A placeholder without a type is an "object": slide body text, bulleted by default.
    private static Shape Shape(uint id, PlaceholderValues? placeholder, params A.Paragraph[] paragraphs)
    {
        var application = new ApplicationNonVisualDrawingProperties(placeholder is { } type ? new PlaceholderShape { Type = type } : new PlaceholderShape { Index = 1U });
        var text = new List<OpenXmlElement> { new A.BodyProperties(), new A.ListStyle() };
        text.AddRange(paragraphs);
        return new Shape(
            new NonVisualShapeProperties(new NonVisualDrawingProperties { Id = id, Name = "Shape " + id }, new NonVisualShapeDrawingProperties(), application),
            new ShapeProperties(),
            new TextBody(text));
    }

    private static A.Paragraph Paragraph(string text, bool bold = false, int level = 0) => new(
        new A.ParagraphProperties { Level = level },
        new A.Run(new A.RunProperties { Bold = bold }, new A.Text(text)));

    private static GraphicFrame Table() => new(
        new NonVisualGraphicFrameProperties(new NonVisualDrawingProperties { Id = 5U, Name = "Table" }, new NonVisualGraphicFrameDrawingProperties(), new ApplicationNonVisualDrawingProperties()),
        new Transform(),
        new A.Graphic(new A.GraphicData(new A.Table(
            new A.TableGrid(new A.GridColumn { Width = 100 }, new A.GridColumn { Width = 100 }),
            Row("Квартал", "План"),
            Row("I", "100"))) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" }));

    private static A.TableRow Row(params string[] cells) =>
        new(cells.Select(text => new A.TableCell(new A.TextBody(new A.BodyProperties(), new A.Paragraph(new A.Run(new A.Text(text)))), new A.TableCellProperties()))) { Height = 100 };
}
