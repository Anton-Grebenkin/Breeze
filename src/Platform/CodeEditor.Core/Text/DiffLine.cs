namespace CodeEditor.Core.Text;

/// <summary>Diff line; line numbers are 1-based, <c>0</c> means the line is absent in that version.</summary>
public readonly record struct DiffLine(DiffKind Kind, string Text, int OldLine, int NewLine);
