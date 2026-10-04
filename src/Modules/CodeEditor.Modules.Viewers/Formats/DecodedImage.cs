namespace CodeEditor.Modules.Viewers.Formats;

/// <summary>A decoded image, ready to show on any thread.</summary>
/// <param name="Picture">The picture for the image element (a frozen <c>BitmapSource</c> in WPF).</param>
/// <param name="Size">Pixel size after the EXIF rotation; shown in the info and used for 100 % zoom.</param>
/// <param name="Decoded">The size of <paramref name="Picture"/>: smaller than <paramref name="Size"/> when reduced for display.</param>
/// <param name="FrameCount">Frames in the file: GIF animation, TIFF pages, ICO icon sizes.</param>
/// <param name="FileLength">File size in bytes.</param>
public sealed record DecodedImage(object Picture, PixelSize Size, PixelSize Decoded, ImageFormat Format, int FrameCount, long FileLength)
{
    public bool IsReduced => Decoded != Size;

    /// <summary>Approximate memory footprint: 4 bytes per pixel.</summary>
    public long MemoryBytes => Decoded.Pixels * sizeof(int);
}
