namespace CodeEditor.Modules.Browser.Services.Agent;

/// <summary>What the agent is told about a browser tab without calling a tool.</summary>
/// <param name="Id">The tab id for the <c>browser</c> tool.</param>
public readonly record struct BrowserTabInfo(string Id, Uri Url, string Title, bool IsActive, bool IsAgent);
