using CodeEditor.Modules.Viewers.Formats;

namespace CodeEditor.Modules.Viewers.Services;

/// <summary>
/// Image decoder for the viewer. Implemented in the view assembly (WIC); the logic knows nothing about WPF.
/// </summary>
public interface IImageDecoder
{
    /// <summary>
    /// Decodes the first frame (the largest one for ICO), applies the EXIF rotation and reduces large images
    /// (<see cref="ImageDecodeLimits"/>). Called on a background thread; the file is not held after the call.
    /// </summary>
    /// <exception cref="ImageDecodeException">No codec for the format, or the file is damaged.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">No access to the file.</exception>
    DecodedImage Decode(string path, CancellationToken cancellationToken);
}
