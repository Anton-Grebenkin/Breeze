namespace CodeEditor.Core.Documents;

/// <summary>Text range replacement; offset and length refer to the original text.</summary>
public readonly record struct TextReplacement(int Offset, int Length, string Text);
