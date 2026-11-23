namespace CodeEditor.Modules.Browser.ViewModels;

/// <summary>An address the bar offers while the user types: a page from the history or a known site.</summary>
/// <param name="Url">Where it leads.</param>
/// <param name="Title">The page title; empty for a site known only by name.</param>
/// <param name="Address">The address as shown: no scheme, no <c>www.</c>.</param>
public sealed record BrowserSuggestion(string Url, string Title, string Address);
