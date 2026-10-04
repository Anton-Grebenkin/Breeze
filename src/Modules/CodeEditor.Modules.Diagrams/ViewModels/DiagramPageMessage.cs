namespace CodeEditor.Modules.Diagrams.ViewModels;

/// <summary>A message from the preview page (<see cref="DiagramPageMessages.Read"/>).</summary>
/// <param name="Type"><c>ready</c>, <c>zoom</c> or <c>key</c>.</param>
/// <param name="Zoom">The zoom for <c>zoom</c>.</param>
/// <param name="Key">The key action for <c>key</c>: <c>zoomIn</c>, <c>zoomOut</c>, <c>zoomReset</c>.</param>
public sealed record DiagramPageMessage(string Type, double? Zoom, string? Key);
