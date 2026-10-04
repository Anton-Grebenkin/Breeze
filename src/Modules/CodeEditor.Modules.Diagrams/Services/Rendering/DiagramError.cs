namespace CodeEditor.Modules.Diagrams.Services.Rendering;

/// <summary>A diagram error from Mermaid: a parse or render message.</summary>
/// <param name="Message">Mermaid's message as is, in English.</param>
/// <param name="Line">The line in the diagram text, from 1; <c>null</c> if Mermaid gave no line.</param>
public sealed record DiagramError(string Message, int? Line);
