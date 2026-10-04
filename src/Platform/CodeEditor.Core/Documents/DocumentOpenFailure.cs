namespace CodeEditor.Core.Documents;

/// <summary>Why a file did not open as text; decides whether a viewer can show it.</summary>
public enum DocumentOpenFailure
{
    /// <summary>Could not be read: access denied, locked or deleted.</summary>
    Unreadable,

    /// <summary>Zero bytes near the start of the file.</summary>
    Binary,

    /// <summary>Exceeds the text editor's size limit.</summary>
    TooLarge,
}
