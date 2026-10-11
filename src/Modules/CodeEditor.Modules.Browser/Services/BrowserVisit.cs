namespace CodeEditor.Modules.Browser.Services;

/// <summary>A page in the browser history (<see cref="BrowserHistory"/>): its address, title and last visit.</summary>
public sealed record BrowserVisit(string Url, string Title, DateTimeOffset Visited);
