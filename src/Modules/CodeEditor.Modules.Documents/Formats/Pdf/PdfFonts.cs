using PdfSharp.Fonts;

namespace CodeEditor.Modules.Documents.Formats.Pdf;

/// <summary>
/// Installs fonts for PDFsharp. The library reads the font resolver from a global setting that can be set only once
/// per process, so it is set on the first PDF creation, not at editor startup.
/// </summary>
internal static class PdfFonts
{
    private static readonly Lock Gate = new();

    /// <exception cref="InvalidOperationException">No Windows fonts with Cyrillic were found.</exception>
    public static void Install()
    {
        lock (Gate)
        {
            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new WindowsFontResolver(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
            }
        }
    }
}
