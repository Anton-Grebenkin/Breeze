namespace CodeEditor.Modules.Agent.Contracts.Context;

/// <summary>What to tell the agent about the editor state.</summary>
/// <param name="IncludeActiveEditor">The user kept the file chip above the input: report the open file and selection.</param>
/// <param name="Text">The user message text, so a module can pick matching context (files mentioned by name).</param>
public sealed record AgentContextRequest(bool IncludeActiveEditor, string Text = "");
