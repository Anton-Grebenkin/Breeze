using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Services;
using CodeEditor.Modules.Viewers.Tests.Infrastructure;
using CodeEditor.Modules.Viewers.Wpf.Services;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// Decoder on Windows codecs with real files: size and format, EXIF photo rotation, downscaling a large image, the
/// largest icon size, GIF frames, damaged files and WebP without a codec; the file is free after reading.
/// </summary>
public sealed class WicImageDecoderTests : IDisposable
{
    // Minimal lossless 1×1 WebP, the same one browsers use to detect format support.
    private const string TinyWebP = "UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA==";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("codeeditor-viewers-");
    private readonly WicImageDecoder _decoder = new();

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public void Png_IsDecoded_WithItsSizeAndFormat_AndFrozen()
    {
        var path = Write("dot.png", TestImages.Png(TestImages.Solid(3, 2, Colors.Red)));

        var image = Decode(path);

        Assert.Equal((new PixelSize(3, 2), ImageFormat.Png, 1), (image.Size, image.Format, image.FrameCount));
        Assert.Equal(new FileInfo(path).Length, image.FileLength);
        var picture = Assert.IsAssignableFrom<BitmapSource>(image.Picture);
        Assert.True(picture.IsFrozen);
        Assert.False(image.IsReduced);
    }

    // The sensor shot the frame sideways (left half red) and wrote tag 6: it must be shown rotated 90° clockwise.
    [Fact]
    public void Photo_IsTurnedByItsExifOrientation()
    {
        var path = Write("photo.jpg", TestImages.JpegWithOrientation(TestImages.Halves(16, 8, Colors.Red, Colors.Blue), 6));

        var image = Decode(path);

        var picture = (BitmapSource)image.Picture;
        Assert.Equal(new PixelSize(8, 16), image.Size);
        Assert.Equal((8, 16), (picture.PixelWidth, picture.PixelHeight));
        Assert.True(IsMostly(TestImages.PixelAt(picture, 4, 2), Colors.Red));
        Assert.True(IsMostly(TestImages.PixelAt(picture, 4, 13), Colors.Blue));
    }

    [Fact]
    public void ImageWiderThanATexture_IsDecodedReduced()
    {
        var path = Write("panorama.png", TestImages.Png(TestImages.Solid(9000, 4, Colors.Green)));

        var image = Decode(path);

        Assert.Equal(new PixelSize(9000, 4), image.Size);
        Assert.Equal(new PixelSize(ImageDecodeLimits.MaxSide, 4), image.Decoded);
        Assert.Equal(ImageDecodeLimits.MaxSide, ((BitmapSource)image.Picture).PixelWidth);
        Assert.True(image.IsReduced);
    }

    [Fact]
    public void Icon_ShowsItsLargestSize()
    {
        var path = Write("app.ico", TestImages.Icon(TestImages.Solid(16, 16, Colors.Red), TestImages.Solid(48, 48, Colors.Blue), TestImages.Solid(32, 32, Colors.Green)));

        var image = Decode(path);

        Assert.Equal((new PixelSize(48, 48), ImageFormat.Ico, 3), (image.Size, image.Format, image.FrameCount));
    }

    [Fact]
    public void AnimatedGif_CountsItsFrames()
    {
        var frames = new[] { Colors.Red, Colors.Green, Colors.Blue }.Select(color => BitmapFrame.Create(TestImages.Solid(4, 4, color))).ToArray();
        var path = Write("anim.gif", TestImages.Encode(new GifBitmapEncoder(), frames));

        var image = Decode(path);

        Assert.Equal((ImageFormat.Gif, 3, new PixelSize(4, 4)), (image.Format, image.FrameCount, image.Size));
    }

    [Fact]
    public void DamagedPng_IsReportedAsDamaged()
    {
        var png = TestImages.Png(TestImages.Solid(8, 8, Colors.Red));
        var path = Write("broken.png", png[..20]);

        var failure = Assert.Throws<ImageDecodeException>(() => Decode(path));

        Assert.Equal((ImageDecodeFailure.Damaged, ImageFormat.Png), (failure.Failure, failure.Format));
    }

    [Fact]
    public void NotAnImage_IsDamaged()
    {
        var path = Write("notes.png", "это текст, а не картинка"u8.ToArray());

        var failure = Assert.Throws<ImageDecodeException>(() => Decode(path));

        Assert.Equal((ImageDecodeFailure.Damaged, ImageFormat.Unknown), (failure.Failure, failure.Format));
    }

    // The Windows WebP codec is a Microsoft Store extension: without it a clear refusal, with it an image.
    [Fact]
    public void WebP_IsDecodedWithTheCodec_OrNeedsIt()
    {
        var path = Write("tiny.webp", Convert.FromBase64String(TinyWebP));

        try
        {
            var image = Decode(path);
            Assert.Equal((ImageFormat.WebP, new PixelSize(1, 1)), (image.Format, image.Size));
        }
        catch (ImageDecodeException failure)
        {
            Assert.Equal((ImageDecodeFailure.NoCodec, ImageFormat.WebP), (failure.Failure, failure.Format));
        }
    }

    [Fact]
    public void File_IsFreeAfterDecoding()
    {
        var path = Write("dot.png", TestImages.Png(TestImages.Solid(2, 2, Colors.Red)));

        Decode(path);

        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    [Fact]
    public void MissingFile_IsAnIoError() =>
        Assert.Throws<FileNotFoundException>(() => Decode(Path.Combine(_folder.FullName, "нет.png")));

    private static bool IsMostly(Color actual, Color expected) =>
        Math.Abs(actual.R - expected.R) < 64 && Math.Abs(actual.G - expected.G) < 64 && Math.Abs(actual.B - expected.B) < 64;

    private DecodedImage Decode(string path) => _decoder.Decode(path, TestContext.Current.CancellationToken);

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_folder.FullName, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
