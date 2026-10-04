using System.Globalization;

namespace CodeEditor.Modules.Viewers.Formats;

/// <summary>Image size in pixels.</summary>
public readonly record struct PixelSize(int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public long Pixels => (long)Width * Height;

    /// <summary>The same sides swapped: the image rotated by 90°.</summary>
    public PixelSize Transpose() => new(Height, Width);

    /// <summary>"1920x1080" for logs and debugging; the user sees the size formatted through resources.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Width}x{Height}");
}
