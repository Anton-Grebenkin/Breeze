namespace CodeEditor.Modules.Viewers.Services;

/// <summary>A message from a viewer page (<see cref="ViewerPageMessages.Read"/>).</summary>
/// <param name="Type">
/// <c>ready</c>: the page loaded; <c>size</c>: SVG drawing size; <c>zoom</c>: wheel or fit zoom; <c>metadata</c>:
/// duration and frame size; <c>error</c>: the drawing or recording cannot be shown.
/// </param>
public sealed record ViewerPageMessage(string Type)
{
    public double? Width { get; init; }

    public double? Height { get; init; }

    /// <summary>The zoom for <c>zoom</c>.</summary>
    public double? Value { get; init; }

    /// <summary>Fit-to-window zoom: the page adjusts it itself when the window resizes.</summary>
    public bool Fit { get; init; }

    /// <summary>Recording duration in seconds for <c>metadata</c>.</summary>
    public double? Duration { get; init; }
}
