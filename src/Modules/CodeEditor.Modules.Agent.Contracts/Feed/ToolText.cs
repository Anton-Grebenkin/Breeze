namespace CodeEditor.Modules.Agent.Contracts.Feed;

/// <summary>Shared helpers for tool rows: file name from a path. For a count with a noun use Core's <c>Plural.Format</c>.</summary>
public static class ToolText
{
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>File name from a path with any separators: <c>src/App/Program.cs</c> → <c>Program.cs</c>.</summary>
    public static string FileName(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var trimmed = path.TrimEnd(Separators);
        var slash = trimmed.LastIndexOfAny(Separators);
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }
}
