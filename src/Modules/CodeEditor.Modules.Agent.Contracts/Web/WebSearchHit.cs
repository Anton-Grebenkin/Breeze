namespace CodeEditor.Modules.Agent.Contracts.Web;

/// <summary>One search result: the page title, its address and the excerpt the engine shows.</summary>
public sealed record WebSearchHit(string Title, string Url, string Snippet);
