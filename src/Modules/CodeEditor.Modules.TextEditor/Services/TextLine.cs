namespace CodeEditor.Modules.TextEditor.Services;

/// <summary>
/// A text line by offsets: <see cref="End"/> excludes the line break, <see cref="NextStart"/> starts the next line.
/// </summary>
public readonly record struct TextLine(int Start, int End, int NextStart);
