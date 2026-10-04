namespace CodeEditor.Modules.Diagrams.ViewModels;

/// <summary>
/// Preview zoom: buttons and keys step like a browser (… 90 %, 100 %, 110 %, 125 % …); <c>Ctrl</c>+wheel and fit give
/// any value within <see cref="Min"/>–<see cref="Max"/>.
/// </summary>
public static class DiagramZoom
{
    public const double Min = 0.1;
    public const double Max = 8;

    // Wheel zoom may sit just off a step; the tolerance keeps e.g. 1.0000001 from treating step 1 as the next one.
    private const double Tolerance = 0.001;

    private static readonly double[] Steps = [0.1, 0.25, 0.33, 0.5, 0.67, 0.75, 0.8, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4, 5, 6, 8];

    public static double Clamp(double zoom) => double.IsFinite(zoom) ? Math.Clamp(zoom, Min, Max) : 1;

    /// <summary>The next larger step.</summary>
    public static double In(double zoom) => Steps.FirstOrDefault(step => step > zoom + Tolerance, Max);

    /// <summary>The next smaller step.</summary>
    public static double Out(double zoom) => Steps.LastOrDefault(step => step < zoom - Tolerance, Min);
}
