namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>A new file from a patch (<c>*** Add File</c>): workspace-relative path and full text.</summary>
public sealed record PatchAddition(string Path, string Content);
