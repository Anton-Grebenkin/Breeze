namespace CodeEditor.Modules.Diagrams.Services.Export;

/// <summary>
/// PNG size: the scale relative to the diagram size and a limit on the longer side in pixels. A large diagram shrinks
/// to the limit; a small one is not enlarged beyond the scale.
/// </summary>
public readonly record struct DiagramPngSize(double Scale, int MaxSide)
{
    /// <summary>Export to a file: twice as large, sharp on high-DPI screens and in documents.</summary>
    public static DiagramPngSize Export { get; } = new(2, 8000);

    /// <summary>
    /// An image for the model: models downscale images over ~2000 pixels anyway, and extra pixels cost tokens.
    /// </summary>
    public static DiagramPngSize Model { get; } = new(2, 2000);
}
