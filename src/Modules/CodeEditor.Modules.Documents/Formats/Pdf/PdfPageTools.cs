using CodeEditor.Modules.Documents.Resources;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CodeEditor.Modules.Documents.Formats.Pdf;

/// <summary>
/// PDF pages without relayout (PDFsharp): combining files and picking pages. Pages are copied as is, with fonts,
/// images and links; bookmarks and forms of the source files are not carried over.
/// </summary>
internal static class PdfPageTools
{
    /// <exception cref="InvalidDataException">The file is not a PDF or is password-protected.</exception>
    public static int PageCount(byte[] bytes)
    {
        using var document = Open(bytes);
        return document.PageCount;
    }

    /// <summary>A new PDF from the sources' pages in order; page numbers start at 1.</summary>
    /// <exception cref="InvalidDataException">A source is not a PDF or is password-protected.</exception>
    public static byte[] Combine(IReadOnlyList<(byte[] Bytes, IReadOnlyList<int> Pages)> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        using var output = new PdfDocument();
        foreach (var (bytes, pages) in sources)
        {
            using var input = Open(bytes);
            foreach (var page in pages)
            {
                output.AddPage(input.Pages[page - 1]);
            }
        }

        using var stream = new MemoryStream();
        output.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static PdfDocument Open(byte[] bytes)
    {
        try
        {
            return PdfReader.Open(new MemoryStream(bytes, writable: false), PdfDocumentOpenMode.Import);
        }
        catch (Exception exception) when (exception is PdfReaderException or InvalidOperationException or NotImplementedException or ArgumentException)
        {
            throw new InvalidDataException(Strings.PdfCannotImport + " " + exception.Message, exception);
        }
    }
}
