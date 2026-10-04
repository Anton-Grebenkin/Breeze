using System.Windows;

namespace CodeEditor.Shell.Wpf.Input;

/// <summary>
/// Full file paths in drags between the app's views: explorer rows and editor tabs carry them to the chat, the editor
/// and the tree. They travel in their own <see cref="Format"/>, not <see cref="DataFormats.FileDrop"/>, so dropping a
/// project file onto Windows Explorer or the desktop can't move it out of the project by accident.
/// </summary>
public static class FileDragData
{
    public const string Format = "CodeEditor.FilePaths";

    public static void SetPaths(DataObject data, IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(paths);
        data.SetData(Format, paths.ToArray());
    }

    /// <summary>Paths dragged inside the app; <c>null</c> for other drags.</summary>
    public static string[]? GetInternalPaths(IDataObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.GetDataPresent(Format) ? data.GetData(Format) as string[] : null;
    }

    /// <summary>Paths dragged inside the app or files dragged from Windows.</summary>
    public static string[]? GetPaths(IDataObject data) =>
        GetInternalPaths(data) ?? (data.GetDataPresent(DataFormats.FileDrop) ? data.GetData(DataFormats.FileDrop) as string[] : null);

    public static bool HasPaths(IDataObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.GetDataPresent(Format) || data.GetDataPresent(DataFormats.FileDrop);
    }
}
