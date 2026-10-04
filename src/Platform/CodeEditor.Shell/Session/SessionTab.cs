namespace CodeEditor.Shell.Session;

/// <summary>An open tab: file path, preview flag and 0-based editor group index (ADR 0031).</summary>
public sealed record SessionTab(string Path, bool IsPreview = false, int Group = 0);
