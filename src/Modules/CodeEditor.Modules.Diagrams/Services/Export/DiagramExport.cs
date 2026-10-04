namespace CodeEditor.Modules.Diagrams.Services.Export;

/// <summary>Export result: the written files (full paths) and the diagrams that failed to render.</summary>
public sealed record DiagramExport(IReadOnlyList<string> Written, IReadOnlyList<DiagramProblem> Problems);
