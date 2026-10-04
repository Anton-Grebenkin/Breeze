namespace CodeEditor.Modules.Viewers.Formats;

/// <summary>
/// The EXIF Orientation tag (274): a phone stores the photo as the sensor lay and records how to rotate it. Browsers
/// and Windows viewers rotate by the tag; otherwise a portrait photo lies on its side.
/// </summary>
public static class ExifOrientation
{
    /// <summary>The WIC metadata query for the tag.</summary>
    public const string MetadataQuery = "System.Photo.Orientation";

    private const int MinValue = 1;
    private const int MaxValue = 8;

    // EXIF values 1-8: where row 0 and column 0 of the photo are. 5 and 7 are diagonal flips.
    private static readonly ImageOrientation[] ByValue =
    [
        ImageOrientation.Upright,
        new(0, FlipHorizontal: true),
        new(ImageOrientation.HalfTurn, FlipHorizontal: false),
        new(ImageOrientation.HalfTurn, FlipHorizontal: true),
        new(ImageOrientation.ThreeQuarterTurn, FlipHorizontal: true),
        new(ImageOrientation.QuarterTurn, FlipHorizontal: false),
        new(ImageOrientation.QuarterTurn, FlipHorizontal: true),
        new(ImageOrientation.ThreeQuarterTurn, FlipHorizontal: false),
    ];

    /// <summary>The rotation for a tag value; an unknown or missing value means no rotation.</summary>
    public static ImageOrientation From(int? value) =>
        value is >= MinValue and <= MaxValue ? ByValue[value.Value - MinValue] : ImageOrientation.Upright;
}
