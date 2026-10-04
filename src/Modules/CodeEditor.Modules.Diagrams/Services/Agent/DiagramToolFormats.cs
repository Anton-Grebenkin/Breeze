using CodeEditor.Modules.Diagrams.Services.Export;

namespace CodeEditor.Modules.Diagrams.Services.Agent;

/// <summary>The <c>format</c> argument of the <c>diagram</c> tool: <c>svg</c> (default) or <c>png</c>, optionally with a dot.</summary>
public static class DiagramToolFormats
{
    public const string Svg = "svg";
    public const string Png = "png";

    /// <returns>The format; <c>null</c> if unknown.</returns>
    public static DiagramFormat? TryParse(string? format)
    {
        var name = format.AsSpan().Trim().TrimStart('.');
        if (name.IsEmpty || name.Equals(Svg, StringComparison.OrdinalIgnoreCase))
        {
            return DiagramFormat.Svg;
        }

        return name.Equals(Png, StringComparison.OrdinalIgnoreCase) ? DiagramFormat.Png : null;
    }
}
