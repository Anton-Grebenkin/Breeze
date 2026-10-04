namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>Helper request usage since the previous turn (<see cref="HelperUsage"/>).</summary>
public readonly record struct HelperUsageTotals(long InputTokens, long OutputTokens, long CachedInputTokens);
