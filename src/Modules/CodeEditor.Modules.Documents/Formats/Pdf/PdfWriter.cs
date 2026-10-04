using CodeEditor.Modules.Documents.Model;
using MigraDoc.Rendering;

namespace CodeEditor.Modules.Documents.Formats.Pdf;

/// <summary>A new PDF from document blocks: MigraDoc layout, PDFsharp output, Windows fonts with Cyrillic.</summary>
internal static class PdfWriter
{
    /// <exception cref="InvalidOperationException">No Windows fonts with Cyrillic were found.</exception>
    public static byte[] Create(RichDocument document)
    {
        PdfFonts.Install();
        var renderer = new PdfDocumentRenderer { Document = MigraDocBlocks.Create(document) };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return stream.ToArray();
    }
}
