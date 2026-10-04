namespace CodeEditor.Modules.Agent.Services.Web;

/// <summary>Web search result: the model's digest and the sources it cited.</summary>
public sealed record WebSearchResult(string Answer, IReadOnlyList<WebSource> Sources);
