namespace CodeEditor.Modules.Viewers.Formats;

/// <summary>
/// The maximum size an image is decoded at for display. A pixel takes 4 bytes: a 48 MP photo would take 190 MB against
/// a 150 MB budget for the whole editor. So an image larger than <see cref="MaxPixels"/> (64 MB in memory) or with a
/// side longer than <see cref="MaxSide"/> (GPU texture limit) is decoded reduced, keeping the aspect ratio; the decoder
/// scales it itself, so the full size never reaches memory. On screen it still takes the full-size area.
/// </summary>
public static class ImageDecodeLimits
{
    public const long MaxPixels = 4096L * 4096;
    public const int MaxSide = 8192;

    /// <summary>The size to decode at; equals the source size when the image is within limits.</summary>
    public static PixelSize For(PixelSize size)
    {
        if (size.IsEmpty)
        {
            return size;
        }

        var scale = Math.Min(Math.Sqrt((double)MaxPixels / size.Pixels), (double)MaxSide / Math.Max(size.Width, size.Height));
        return scale >= 1
            ? size
            : new PixelSize(Math.Max(1, (int)Math.Round(size.Width * scale)), Math.Max(1, (int)Math.Round(size.Height * scale)));
    }
}
