namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>A parsed <c>apply_patch</c> patch: edits to existing files and new files.</summary>
public sealed record ParsedPatch(IReadOnlyList<FileEdit> Edits, IReadOnlyList<PatchAddition> Additions);
