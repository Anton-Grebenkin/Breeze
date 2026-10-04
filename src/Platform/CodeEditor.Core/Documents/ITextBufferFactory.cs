namespace CodeEditor.Core.Documents;

/// <summary>
/// Creates document buffers; the editor module registers the implementation (ADR 0003). Called on a background
/// thread so splitting a large file into lines does not block the UI; the resulting buffer belongs to the UI thread.
/// </summary>
public interface ITextBufferFactory
{
    ITextBuffer Create(string text);
}
