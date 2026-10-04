namespace CodeEditor.Core.Logging;

/// <summary>Log location, for the "open log" and "show logs folder" developer commands.</summary>
public interface ILogFiles
{
    string Folder { get; }

    /// <summary>The file being written now; <c>null</c> when not open yet or unavailable.</summary>
    string? CurrentFile { get; }
}
