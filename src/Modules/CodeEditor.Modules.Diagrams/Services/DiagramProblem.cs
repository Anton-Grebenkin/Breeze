namespace CodeEditor.Modules.Diagrams.Services;

/// <summary>A diagram error at a file line (<see cref="DiagramErrors.Locate"/>).</summary>
/// <param name="Index">The diagram number in the file, from 1.</param>
/// <param name="Line">The file line, from 1.</param>
/// <param name="Message">Mermaid's message without its own line numbers (they count from the diagram start).</param>
public sealed record DiagramProblem(int Index, int Line, string Message);
