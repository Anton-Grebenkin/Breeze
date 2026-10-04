namespace CodeEditor.Core.Files;

/// <summary>
/// All non-excluded files of the open folder, for quick open (<c>Ctrl+P</c>), file search and agent tools.
/// Built in the background when a folder opens and kept up to date by the file watcher.
/// </summary>
public interface IFileIndex
{
    /// <summary>Immutable snapshot; safe to read from any thread.</summary>
    IReadOnlyList<IndexedFile> Files { get; }

    /// <summary>Raised on a background thread.</summary>
    event EventHandler? Changed;

    /// <summary>Completes when the initial scan is done.</summary>
    Task WhenReady { get; }
}
