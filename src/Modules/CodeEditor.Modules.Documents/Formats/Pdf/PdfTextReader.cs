using CodeEditor.Modules.Documents.Resources;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace CodeEditor.Modules.Documents.Formats.Pdf;

/// <summary>
/// PDF text by page (PdfPig): letters are joined into lines in content order, with line breaks and spaces by their
/// position. A scan without a text layer yields empty pages; there is no text recognition.
/// </summary>
internal static class PdfTextReader
{
    /// <exception cref="InvalidDataException">The file is not a PDF or is encrypted.</exception>
    public static int PageCount(byte[] bytes) => Open(bytes, static document => document.NumberOfPages);

    /// <summary>Text of the pages numbered in <paramref name="pages"/> (from 1), in order.</summary>
    /// <exception cref="InvalidDataException">The file is not a PDF or is encrypted.</exception>
    public static IReadOnlyList<(int Number, string Text)> Read(byte[] bytes, IReadOnlyList<int> pages, CancellationToken cancellationToken) =>
        Open(bytes, document =>
        {
            var result = new List<(int, string)>(pages.Count);
            foreach (var number in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add((number, ContentOrderTextExtractor.GetText(document.GetPage(number)).TrimEnd()));
            }

            return result;
        });

    private static T Open<T>(byte[] bytes, Func<PdfDocument, T> read)
    {
        try
        {
            using var document = PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = true });
            return read(document);
        }
        catch (PdfDocumentEncryptedException exception)
        {
            throw new InvalidDataException(Strings.PdfEncrypted, exception);
        }
        catch (Exception exception) when (exception is PdfDocumentFormatException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException(Strings.NotPdf, exception);
        }
    }
}
