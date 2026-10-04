namespace CodeEditor.Modules.Diagrams.Services.Rendering;

/// <summary>
/// The diagram renderer does not work: WebView2 is unavailable, Mermaid did not load (no network and no local copy) or
/// rendering timed out. The message is for people and the model.
/// </summary>
public sealed class DiagramRendererException : Exception
{
    public DiagramRendererException()
    {
    }

    public DiagramRendererException(string message)
        : base(message)
    {
    }

    public DiagramRendererException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
