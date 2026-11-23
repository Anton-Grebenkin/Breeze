namespace CodeEditor.Modules.Browser.Services.Tabs;

/// <summary>A tab as kept between runs (<see cref="BrowserTabStore"/>).</summary>
public sealed record BrowserTabRecord(string Url, string Title, bool IsPinned, bool IsActive);
