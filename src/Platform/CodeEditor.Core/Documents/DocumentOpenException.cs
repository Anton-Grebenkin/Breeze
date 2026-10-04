namespace CodeEditor.Core.Documents;

/// <summary>
/// The file cannot be opened as text: binary, too large or unreadable. The message is user-facing.
/// </summary>
public sealed class DocumentOpenException : IOException
{
    public DocumentOpenException(string message)
        : this(message, DocumentOpenFailure.Unreadable)
    {
    }

    public DocumentOpenException(string message, Exception innerException)
        : base(message, innerException) => Failure = DocumentOpenFailure.Unreadable;

    public DocumentOpenException(string message, DocumentOpenFailure failure)
        : base(message) => Failure = failure;

    /// <summary>Why opening failed: binary and too-large files can still go to a viewer.</summary>
    public DocumentOpenFailure Failure { get; }
}
