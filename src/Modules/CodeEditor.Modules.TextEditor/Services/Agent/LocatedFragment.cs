namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>A located edit fragment: range in the file text, replacement, and a warning if the match was inexact.</summary>
public sealed record LocatedFragment(int Offset, int Length, string NewText, string? Warning);
