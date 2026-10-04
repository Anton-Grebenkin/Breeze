namespace CodeEditor.Modules.Agent.Services.Prompts;

/// <summary>A piece of the model's text stream: plain text, reasoning, or the end of reasoning.</summary>
public readonly record struct TaggedText(TaggedTextKind Kind, string Text);
