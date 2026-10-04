namespace CodeEditor.Core.Text;

/// <summary>Diff hunk: changed lines with surrounding context, like <c>@@ -12,7 +12,8 @@</c>.</summary>
public sealed record DiffHunk(int OldStart, int NewStart, IReadOnlyList<DiffLine> Lines);
