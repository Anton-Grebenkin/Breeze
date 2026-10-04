namespace CodeEditor.Modules.Agent.Services.History;

/// <summary>A folder chat list row: the title is the start of the first question; Updated is the last answer time.</summary>
public sealed record ChatSummary(string Id, string Title, DateTimeOffset Created, DateTimeOffset Updated);
