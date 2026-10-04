namespace CodeEditor.Modules.Viewers.Formats;

/// <summary>
/// How to orient an image for display: first flip horizontally (<see cref="FlipHorizontal"/>), then rotate clockwise
/// by <see cref="Rotation"/> degrees (0, 90, 180 or 270).
/// </summary>
public readonly record struct ImageOrientation(int Rotation, bool FlipHorizontal)
{
    public const int QuarterTurn = 90;
    public const int HalfTurn = 180;
    public const int ThreeQuarterTurn = 270;

    public static ImageOrientation Upright { get; } = new(0, FlipHorizontal: false);

    public bool IsUpright => Rotation == 0 && !FlipHorizontal;

    /// <summary>A quarter turn swaps width and height.</summary>
    public bool SwapsSides => Rotation is QuarterTurn or ThreeQuarterTurn;
}
