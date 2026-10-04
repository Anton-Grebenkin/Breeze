namespace CodeEditor.Modules.Viewers.Formats;

/// <summary>
/// Detects the image format from the file signature. The tab info names the real format, and the missing codec message
/// names the format that lacks a codec (WebP).
/// </summary>
public static class ImageFormats
{
    /// <summary>How many leading bytes detection needs.</summary>
    public const int HeaderLength = 12;

    private const int RiffTagOffset = 8;

    public static ImageFormat Detect(ReadOnlySpan<byte> header) => header switch
    {
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => ImageFormat.Png,
        [0xFF, 0xD8, 0xFF, ..] => ImageFormat.Jpeg,
        [0x47, 0x49, 0x46, 0x38, 0x37 or 0x39, 0x61, ..] => ImageFormat.Gif,
        [0x42, 0x4D, ..] => ImageFormat.Bmp,
        [0x00, 0x00, 0x01, 0x00, ..] => ImageFormat.Ico,
        [0x49, 0x49, 0x2A, 0x00, ..] or [0x4D, 0x4D, 0x00, 0x2A, ..] => ImageFormat.Tiff,
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, ..] when header[RiffTagOffset..].StartsWith("WEBP"u8) => ImageFormat.WebP,
        _ => ImageFormat.Unknown,
    };

    /// <summary>The format name for the info, e.g. "PNG"; an unknown format shows the upper-case file extension.</summary>
    public static string Name(ImageFormat format, string path) => format switch
    {
        ImageFormat.Png => "PNG",
        ImageFormat.Jpeg => "JPEG",
        ImageFormat.Gif => "GIF",
        ImageFormat.Bmp => "BMP",
        ImageFormat.Ico => "ICO",
        ImageFormat.Tiff => "TIFF",
        ImageFormat.WebP => "WebP",
        _ => Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
    };

    /// <summary>The file may have several frames while only the first is shown: GIF animation, TIFF pages.</summary>
    public static bool HasFrames(ImageFormat format) => format is ImageFormat.Gif or ImageFormat.Tiff or ImageFormat.WebP;
}
