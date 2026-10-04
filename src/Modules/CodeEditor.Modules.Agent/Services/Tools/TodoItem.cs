namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>An agent plan item: a short title (3–7 words) and a status.</summary>
public sealed record TodoItem(string Id, string Title, TodoStatus Status);
