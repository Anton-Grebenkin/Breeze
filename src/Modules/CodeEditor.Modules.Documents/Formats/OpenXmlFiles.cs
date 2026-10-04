using CodeEditor.Modules.Documents.Resources;
using DocumentFormat.OpenXml.Packaging;

namespace CodeEditor.Modules.Documents.Formats;

/// <summary>
/// Opens Office documents: a damaged file or one of another format throws <see cref="InvalidDataException"/> with a
/// clear message instead of package and archive exceptions. Editing needs a growable stream: a copy in a new
/// <see cref="MemoryStream"/>.
/// </summary>
internal static class OpenXmlFiles
{
    /// <exception cref="InvalidDataException">Not a Word document.</exception>
    public static WordprocessingDocument OpenWord(Stream stream, bool editable) => Open(() => WordprocessingDocument.Open(stream, editable));

    /// <exception cref="InvalidDataException">Not an Excel workbook.</exception>
    public static SpreadsheetDocument OpenExcel(Stream stream, bool editable) => Open(() => SpreadsheetDocument.Open(stream, editable));

    /// <exception cref="InvalidDataException">Not a PowerPoint presentation.</exception>
    public static PresentationDocument OpenPowerPoint(Stream stream, bool editable) => Open(() => PresentationDocument.Open(stream, editable));

    /// <summary>A stream for editing: a copy of the bytes that can grow on save.</summary>
    public static MemoryStream Editable(byte[] bytes)
    {
        var stream = new MemoryStream(bytes.Length + bytes.Length / 4);
        stream.Write(bytes);
        stream.Position = 0;
        return stream;
    }

    public static InvalidDataException NotDocument(Exception? inner = null) => new(Strings.NotOfficeDocument, inner);

    private static T Open<T>(Func<T> open)
    {
        try
        {
            return open();
        }
        catch (Exception exception) when (exception is OpenXmlPackageException or FileFormatException or InvalidDataException or InvalidOperationException)
        {
            throw NotDocument(exception);
        }
    }
}
