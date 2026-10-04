namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>Images for viewer UI tests: an uncompressed gradient BMP, a format writable without libraries.</summary>
public static class SampleImages
{
    private const int HeaderSize = 54;
    private const int InfoHeaderSize = 40;
    private const int BytesPerPixel = 3;
    private const int PixelsPerMeter = 2835;

    public static void WriteBmp(string path, int width, int height)
    {
        var stride = (width * BytesPerPixel + 3) & ~3;
        var size = stride * height;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("BM"u8);
        writer.Write(HeaderSize + size);
        writer.Write(0);
        writer.Write(HeaderSize);
        writer.Write(InfoHeaderSize);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)(BytesPerPixel * 8));
        writer.Write(0);
        writer.Write(size);
        writer.Write(PixelsPerMeter);
        writer.Write(PixelsPerMeter);
        writer.Write(0);
        writer.Write(0);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                writer.Write((byte)(x * 255 / width));
                writer.Write((byte)(y * 255 / height));
                writer.Write((byte)200);
            }

            writer.Write(new byte[stride - width * BytesPerPixel]);
        }
    }
}
