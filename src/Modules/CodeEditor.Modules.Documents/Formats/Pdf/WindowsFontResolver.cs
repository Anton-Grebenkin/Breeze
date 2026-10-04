using System.Collections.Concurrent;
using CodeEditor.Modules.Documents.Resources;
using PdfSharp.Fonts;

namespace CodeEditor.Modules.Documents.Formats.Pdf;

/// <summary>
/// PDF fonts from the Windows font folder: Segoe UI for text (else Arial, Tahoma), Consolas for code (else Courier
/// New); all have Cyrillic. PDFsharp embeds only the glyphs used. Any other family name maps to the text font, a
/// monospace one to the code font; a missing bold or italic file is simulated.
/// </summary>
internal sealed class WindowsFontResolver : IFontResolver
{
    private const string FontExtension = ".ttf";

    // Regular, bold, italic, bold italic.
    private static readonly string[][] TextFaces = [["segoeui", "segoeuib", "segoeuii", "segoeuiz"], ["arial", "arialbd", "ariali", "arialbi"], ["tahoma", "tahomabd", "tahoma", "tahomabd"]];
    private static readonly string[][] CodeFaces = [["consola", "consolab", "consolai", "consolaz"], ["cour", "courbd", "couri", "courbi"]];

    private readonly string _folder;
    private readonly string[] _text;
    private readonly string[] _code;
    private readonly ConcurrentDictionary<string, byte[]> _fonts = new(StringComparer.OrdinalIgnoreCase);

    /// <exception cref="InvalidOperationException">The folder has none of the fonts.</exception>
    public WindowsFontResolver(string folder)
    {
        _folder = folder;
        _text = TextFaces.FirstOrDefault(faces => Exists(faces[0])) ?? throw new InvalidOperationException(Strings.NoPdfFonts);
        _code = CodeFaces.FirstOrDefault(faces => Exists(faces[0])) ?? _text;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        var faces = IsMonospace(familyName) ? _code : _text;
        var face = faces[(bold ? 1 : 0) + (italic ? 2 : 0)];
        return Exists(face) ? new FontResolverInfo(face) : new FontResolverInfo(faces[0], bold, italic);
    }

    public byte[]? GetFont(string faceName) => _fonts.GetOrAdd(faceName, name => File.ReadAllBytes(PathOf(name)));

    private static bool IsMonospace(string familyName) =>
        familyName.Contains("Consolas", StringComparison.OrdinalIgnoreCase)
        || familyName.Contains("Courier", StringComparison.OrdinalIgnoreCase)
        || familyName.Contains("Mono", StringComparison.OrdinalIgnoreCase);

    private bool Exists(string face) => File.Exists(PathOf(face));

    private string PathOf(string face) => Path.Combine(_folder, face + FontExtension);
}
