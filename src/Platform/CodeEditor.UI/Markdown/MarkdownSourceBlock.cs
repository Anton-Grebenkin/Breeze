using MdBlock = Markdig.Syntax.Block;

namespace CodeEditor.UI.Markdown;

/// <summary>A top-level Markdown block with its source text; equal text means an equal document block.</summary>
public readonly record struct MarkdownSourceBlock(string Source, MdBlock Block);
