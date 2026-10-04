namespace CodeEditor.Modules.Search.Services.Matching;

/// <summary>
/// A match in a file: 1-based line and column, length, and a preview line where the match starts at
/// <see cref="PreviewStart"/> and spans <see cref="PreviewLength"/> chars (long lines are cut around it).
/// </summary>
public readonly record struct SearchMatch(int Line, int Column, int Length, string Preview, int PreviewStart, int PreviewLength);
