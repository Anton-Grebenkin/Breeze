namespace CodeEditor.Modules.Viewers.Services;

/// <summary>Why an image failed to decode; the advice to the user depends on it.</summary>
public enum ImageDecodeFailure
{
    /// <summary>Windows has no decoder for the format, e.g. WebP without the "WebP Image Extensions".</summary>
    NoCodec,

    /// <summary>The file is damaged or not an image.</summary>
    Damaged,
}
