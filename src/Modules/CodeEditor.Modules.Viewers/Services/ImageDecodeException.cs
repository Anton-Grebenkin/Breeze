using CodeEditor.Modules.Viewers.Formats;

namespace CodeEditor.Modules.Viewers.Services;

/// <summary>The image cannot be decoded: no codec or a damaged file. The message holds the decoder's technical details.</summary>
public sealed class ImageDecodeException : Exception
{
    public ImageDecodeException()
        : this(ImageDecodeFailure.Damaged, ImageFormat.Unknown, string.Empty)
    {
    }

    public ImageDecodeException(string message)
        : this(ImageDecodeFailure.Damaged, ImageFormat.Unknown, message)
    {
    }

    public ImageDecodeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ImageDecodeException(ImageDecodeFailure failure, ImageFormat format, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
        Format = format;
    }

    public ImageDecodeFailure Failure { get; }

    /// <summary>The format from the file signature: WebP without a codec gets different advice.</summary>
    public ImageFormat Format { get; }
}
