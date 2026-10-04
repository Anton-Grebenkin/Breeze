namespace CodeEditor.Shell.Instances;

/// <summary>A request to a running window: come to the front and open the folder or file.</summary>
/// <param name="Path">A file to open, or the window's own folder; <c>null</c> only activates the window.</param>
public sealed record InstanceRequest(string? Path);
