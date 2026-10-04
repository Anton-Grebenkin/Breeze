using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodeEditor.Modules.Viewers.Tests.Infrastructure;

/// <summary>Images for decoder tests: encoded by WPF into the test folder; the icon is assembled from PNGs by hand.</summary>
internal static class TestImages
{
    private const int Dpi = 96;
    private const int BytesPerPixel = 4;
    private const int IconHeaderSize = 6;
    private const int IconEntrySize = 16;
    private const int IconBitCount = 32;

    /// <summary>An image whose left half is <paramref name="left"/> and right half is <paramref name="right"/>.</summary>
    public static BitmapSource Halves(int width, int height, Color left, Color right)
    {
        var pixels = new byte[width * height * BytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = x < width / 2 ? left : right;
                var index = ((y * width) + x) * BytesPerPixel;
                (pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]) = (color.B, color.G, color.R, color.A);
            }
        }

        var source = BitmapSource.Create(width, height, Dpi, Dpi, PixelFormats.Bgra32, null, pixels, width * BytesPerPixel);
        source.Freeze();
        return source;
    }

    public static BitmapSource Solid(int width, int height, Color color) => Halves(width, height, color, color);

    public static byte[] Encode(BitmapEncoder encoder, params BitmapFrame[] frames)
    {
        foreach (var frame in frames)
        {
            encoder.Frames.Add(frame);
        }

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public static byte[] Png(BitmapSource source) => Encode(new PngBitmapEncoder(), BitmapFrame.Create(source));

    /// <summary>JPEG with an EXIF Orientation tag.</summary>
    public static byte[] JpegWithOrientation(BitmapSource source, ushort orientation)
    {
        var metadata = new BitmapMetadata("jpg");
        metadata.SetQuery("/app1/ifd/{ushort=274}", orientation);
        return Encode(new JpegBitmapEncoder { QualityLevel = 100 }, BitmapFrame.Create(source, null, metadata, null));
    }

    /// <summary>An icon of several PNG images, the way 256 px and larger sizes and modern icons are stored.</summary>
    public static byte[] Icon(params BitmapSource[] sizes)
    {
        var images = sizes.Select(Png).ToArray();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)images.Length);
        var offset = IconHeaderSize + (IconEntrySize * images.Length);
        for (var index = 0; index < images.Length; index++)
        {
            writer.Write((byte)sizes[index].PixelWidth);
            writer.Write((byte)sizes[index].PixelHeight);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)IconBitCount);
            writer.Write(images[index].Length);
            writer.Write(offset);
            offset += images[index].Length;
        }

        foreach (var image in images)
        {
            writer.Write(image);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Pixel color of a display-ready image.</summary>
    public static Color PixelAt(BitmapSource source, int x, int y)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixel = new byte[BytesPerPixel];
        converted.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, BytesPerPixel, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }
}
