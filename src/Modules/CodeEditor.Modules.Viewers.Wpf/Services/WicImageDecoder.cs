using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Services;

namespace CodeEditor.Modules.Viewers.Wpf.Services;

/// <summary>
/// Image decoder on Windows codecs (WIC): PNG, JPEG, GIF, BMP, ICO, TIFF and installed extensions such as WebP. The image
/// is fully decoded on a background thread and frozen, and the file is not held. First frame (the largest for ICO),
/// EXIF rotation, large images reduced while decoding (<see cref="ImageDecodeLimits"/>). An image with a damaged color
/// profile is decoded again without the profile.
/// </summary>
public sealed class WicImageDecoder : IImageDecoder
{
    private const int BufferSize = 64 * 1024;
    private const FileShare Sharing = FileShare.ReadWrite | FileShare.Delete;

    public DecodedImage Decode(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, Sharing, BufferSize);
        var format = ImageFormats.Detect(ReadHeader(stream));
        try
        {
            return Decode(stream, format, BitmapCreateOptions.None, cancellationToken);
        }
        catch (Exception exception) when (IsDecodeFailure(exception) && format != ImageFormat.Unknown)
        {
            // A damaged color profile fails the whole decode; without the profile the image usually reads fine.
            stream.Position = 0;
            return DecodeOrExplain(stream, format, cancellationToken, exception);
        }
        catch (Exception exception) when (IsDecodeFailure(exception))
        {
            throw Explain(format, exception);
        }
    }

    private static DecodedImage DecodeOrExplain(FileStream stream, ImageFormat format, CancellationToken cancellationToken, Exception first)
    {
        try
        {
            return Decode(stream, format, BitmapCreateOptions.IgnoreColorProfile, cancellationToken);
        }
        catch (Exception exception) when (IsDecodeFailure(exception))
        {
            throw Explain(format, first);
        }
    }

    private static DecodedImage Decode(FileStream stream, ImageFormat format, BitmapCreateOptions options, CancellationToken cancellationToken)
    {
        var decoder = BitmapDecoder.Create(stream, options, BitmapCacheOption.None);
        var frame = PickFrame(decoder);
        var stored = new PixelSize(frame.PixelWidth, frame.PixelHeight);
        var orientation = ExifOrientation.From(ReadOrientation(frame));
        cancellationToken.ThrowIfCancellationRequested();

        var picture = WicPictures.Prepare(frame, ImageDecodeLimits.For(stored), orientation);
        var size = orientation.SwapsSides ? stored.Transpose() : stored;
        return new DecodedImage(picture, size, new PixelSize(picture.PixelWidth, picture.PixelHeight), format, decoder.Frames.Count, stream.Length);
    }

    // An icon has several sizes: show the largest; other formats show the first frame.
    private static BitmapFrame PickFrame(BitmapDecoder decoder)
    {
        if (decoder.Frames.Count == 0)
        {
            throw new FileFormatException();
        }

        return decoder is IconBitmapDecoder
            ? decoder.Frames.MaxBy(frame => (long)frame.PixelWidth * frame.PixelHeight)!
            : decoder.Frames[0];
    }

    private static int? ReadOrientation(BitmapFrame frame)
    {
        try
        {
            return frame.Metadata is BitmapMetadata metadata && metadata.GetQuery(ExifOrientation.MetadataQuery) is ushort value ? value : null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ArgumentException or COMException)
        {
            // The format has no metadata (BMP) or does not support the query: treat the photo as upright.
            return null;
        }
    }

    private static byte[] ReadHeader(Stream stream)
    {
        var header = new byte[ImageFormats.HeaderLength];
        var length = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Position = 0;
        return header[..length];
    }

    // "No codec" only for a recognized format not built into Windows (WebP); anything else is a damaged file.
    private static ImageDecodeException Explain(ImageFormat format, Exception exception) =>
        exception is NotSupportedException && format == ImageFormat.WebP
            ? new ImageDecodeException(ImageDecodeFailure.NoCodec, format, exception.Message, exception)
            : new ImageDecodeException(ImageDecodeFailure.Damaged, format, exception.Message, exception);

    private static bool IsDecodeFailure(Exception exception) =>
        exception is NotSupportedException or FileFormatException or ArgumentException or COMException or OverflowException or InvalidOperationException;
}
