using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Services;

namespace CodeEditor.Modules.Viewers.Tests.Infrastructure;

/// <summary>Decoder that returns the image or error set by the test and counts calls.</summary>
internal sealed class FakeImageDecoder : IImageDecoder
{
    private int _calls;

    /// <summary>What decoding returns: an image or an exception.</summary>
    public Func<string, DecodedImage> Result { get; set; } = _ => Image(new PixelSize(640, 480));

    public int Calls => _calls;

    /// <summary>An image of the given size; the displayable picture is just an object.</summary>
    public static DecodedImage Image(PixelSize size, PixelSize? decoded = null, ImageFormat format = ImageFormat.Png, int frames = 1, long length = 2048) =>
        new(new object(), size, decoded ?? size, format, frames, length);

    public DecodedImage Decode(string path, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return Result(path);
    }
}
