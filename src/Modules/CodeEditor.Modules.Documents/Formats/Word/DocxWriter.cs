using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>
/// A new Word document from blocks: heading, list, code and table styles (<see cref="WordStyles"/>), A4 page with 2 cm
/// margins. The document title property is the first heading of the text.
/// </summary>
internal static class DocxWriter
{
    private const uint A4Width = 11906;
    private const uint A4Height = 16838;

    /// <summary>2 cm in twentieths of a point; top and bottom margins are signed, left and right unsigned.</summary>
    private const int VerticalMargin = 1134;
    private const uint SideMargin = 1134;
    private const uint HeaderMargin = 709;

    public static byte[] Create(RichDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = word.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = WordStyles.Create();
            var body = new Body();
            main.Document = new Document(body);
            var blocks = new WordBlocks(main, WordStyles.Ensure(main), new WordLists(main));
            body.Append(blocks.Convert(document));
            body.Append(PageSetup());
            if (document.Blocks.FirstOrDefault(block => block.Kind == DocumentBlockKind.Heading) is { } title)
            {
                word.PackageProperties.Title = title.PlainText;
            }
        }

        return stream.ToArray();
    }

    private static SectionProperties PageSetup() => new(
        new PageSize { Width = A4Width, Height = A4Height },
        new PageMargin { Top = VerticalMargin, Right = SideMargin, Bottom = VerticalMargin, Left = SideMargin, Header = HeaderMargin, Footer = HeaderMargin, Gutter = 0U });
}
