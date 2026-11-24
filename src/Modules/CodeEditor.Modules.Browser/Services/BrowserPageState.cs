namespace CodeEditor.Modules.Browser.Services;

/// <summary>A page kept from the last run: shown in the tab at once, loaded when the tab is first shown.</summary>
public sealed record BrowserPageState(Uri Url, string Title);
