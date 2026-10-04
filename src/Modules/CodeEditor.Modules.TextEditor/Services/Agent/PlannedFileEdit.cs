using CodeEditor.Core.Documents;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>One file's edits, ready to apply as a single undo step; warnings flag inexact fragment matches.</summary>
public sealed record PlannedFileEdit(string FullPath, string RelativePath, string OldText, string NewText, IReadOnlyList<TextReplacement> Replacements)
{
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
