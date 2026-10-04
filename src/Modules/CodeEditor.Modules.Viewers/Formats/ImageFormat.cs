namespace CodeEditor.Modules.Viewers.Formats;

/// <summary>Image format detected from the first bytes, not the extension: a ".png" may turn out to be a JPEG.</summary>
public enum ImageFormat
{
    Unknown,
    Png,
    Jpeg,
    Gif,
    Bmp,
    Ico,
    Tiff,
    WebP,
}
