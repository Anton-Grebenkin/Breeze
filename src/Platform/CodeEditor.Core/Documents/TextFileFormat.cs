using System.Text;
using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Documents;

/// <summary>
/// On-disk format: encoding, BOM and line endings. A file is saved in the same format it was opened with.
/// </summary>
public sealed record TextFileFormat(Encoding Encoding, bool HasByteOrderMark, LineEnding LineEnding)
{
    /// <summary>For new files: UTF-8 without BOM, Windows line endings.</summary>
    public static TextFileFormat Default { get; } = new(new UTF8Encoding(false), false, LineEnding.CrLf);

    /// <summary>Status bar label: "UTF-8", "UTF-8 with BOM", "Windows-1251".</summary>
    public string EncodingName => Encoding switch
    {
        UTF8Encoding => HasByteOrderMark ? Strings.EncodingUtf8WithBom : "UTF-8",
        UnicodeEncoding { CodePage: 1201 } => "UTF-16 BE",
        UnicodeEncoding => "UTF-16 LE",
        UTF32Encoding => "UTF-32",
        _ => Encoding.WebName.ToUpperInvariant(),
    };

    public string LineEndingName => LineEnding == LineEnding.CrLf ? "CRLF" : "LF";

    public string NewLine => LineEnding == LineEnding.CrLf ? "\r\n" : "\n";
}
