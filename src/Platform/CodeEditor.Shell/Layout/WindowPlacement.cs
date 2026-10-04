namespace CodeEditor.Shell.Layout;

/// <summary>Main window position and size in DPI-independent units.</summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool IsMaximized);
