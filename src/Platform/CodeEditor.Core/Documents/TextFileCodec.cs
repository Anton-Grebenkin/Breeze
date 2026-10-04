using System.Globalization;
using System.Text;
using System.Text.Unicode;

namespace CodeEditor.Core.Documents;

/// <summary>
/// File bytes to text and back. Encoding is detected like VS Code: BOM first, then strict UTF-8 validation,
/// otherwise the Windows ANSI code page (Windows-1251 on a Russian system). A file without BOM that contains
/// zero bytes is binary.
/// </summary>
public static class TextFileCodec
{
    /// <summary>How many leading bytes to probe for binary content, as git and VS Code do.</summary>
    public const int BinaryProbeLength = 8 * 1024;

    static TextFileCodec() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Decodes file content; returns <c>null</c> for a binary file.</summary>
    public static (string Text, TextFileFormat Format)? Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var (encoding, preambleLength) = DetectByteOrderMark(bytes);
        if (encoding is null)
        {
            if (LooksBinary(bytes))
            {
                return null;
            }

            encoding = Utf8.IsValid(bytes) ? new UTF8Encoding(false) : AnsiEncoding();
        }

        var text = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
        return (text, new TextFileFormat(encoding, preambleLength > 0, DetectLineEnding(text)));
    }

    public static byte[] Encode(string text, TextFileFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(format);

        var preamble = format.HasByteOrderMark ? format.Encoding.GetPreamble() : [];
        var result = new byte[preamble.Length + format.Encoding.GetByteCount(text)];
        preamble.CopyTo(result, 0);
        format.Encoding.GetBytes(text, 0, text.Length, result, preamble.Length);
        return result;
    }

    /// <summary>Line ending of the first line break; a file without line breaks gets the Windows default.</summary>
    public static LineEnding DetectLineEnding(string text)
    {
        var index = text.IndexOf('\n', StringComparison.Ordinal);
        return index switch
        {
            < 0 => LineEnding.CrLf,
            > 0 when text[index - 1] == '\r' => LineEnding.CrLf,
            _ => LineEnding.Lf,
        };
    }

    private static (Encoding? Encoding, int PreambleLength) DetectByteOrderMark(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0xEF, 0xBB, 0xBF, ..] => (new UTF8Encoding(true), 3),
        [0xFF, 0xFE, 0x00, 0x00, ..] => (new UTF32Encoding(bigEndian: false, byteOrderMark: true), 4),
        [0xFF, 0xFE, ..] => (new UnicodeEncoding(bigEndian: false, byteOrderMark: true), 2),
        [0xFE, 0xFF, ..] => (new UnicodeEncoding(bigEndian: true, byteOrderMark: true), 2),
        _ => (null, 0),
    };

    private static bool LooksBinary(ReadOnlySpan<byte> bytes) =>
        bytes[..Math.Min(bytes.Length, BinaryProbeLength)].Contains((byte)0);

    private static Encoding AnsiEncoding() => Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
}
