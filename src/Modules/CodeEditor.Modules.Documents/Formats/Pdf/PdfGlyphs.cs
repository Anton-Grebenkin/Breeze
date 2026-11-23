using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace CodeEditor.Modules.Documents.Formats.Pdf;

/// <summary>
/// Characters the PDF font has no glyph for (emoji, hieroglyphs, some symbols): in the file they come out as empty
/// boxes, with no error. The tool tells the model, so it replaces them instead of handing over a PDF with holes.
/// </summary>
internal static class PdfGlyphs
{
    /// <summary>How many characters to name: the answer needs examples, not the whole list.</summary>
    public const int MaxListed = 12;

    private const double AnySize = 10;

    /// <summary>The text's characters without a glyph in the font, each once, in order of appearance. O(text length).</summary>
    public static IReadOnlyList<string> Missing(string text, string fontFamily)
    {
        ArgumentNullException.ThrowIfNull(text);
        PdfFonts.Install();
        var font = new XFont(fontFamily, AnySize);
        var seen = new HashSet<Rune>();
        var missing = new List<string>();
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune) || Rune.IsControl(rune) || !seen.Add(rune))
            {
                continue;
            }

            if (GlyphHelper.GlyphIndexFromCodePoint(rune.Value, font) == 0)
            {
                missing.Add(rune.ToString());
            }
        }

        return missing;
    }
}
