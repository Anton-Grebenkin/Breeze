namespace CodeEditor.Core.Documents;

/// <summary>
/// Open file. The user and the agent edit its text through the same <see cref="ITextBuffer"/> (ADR 0003).
/// </summary>
public interface IDocument
{
    string FilePath { get; }

    string Name { get; }

    ITextBuffer Buffer { get; }

    TextFileFormat Format { get; }

    /// <summary>Has unsaved edits.</summary>
    bool IsDirty { get; }

    /// <summary>Changed on disk while there are unsaved edits: saving would overwrite those changes.</summary>
    bool HasExternalChanges { get; }

    /// <summary>Deleted or renamed on disk.</summary>
    bool IsDeletedOnDisk { get; }

    /// <summary>
    /// Raised when <see cref="IsDirty"/>, <see cref="HasExternalChanges"/> or <see cref="IsDeletedOnDisk"/> changes.
    /// </summary>
    event EventHandler? StateChanged;
}
