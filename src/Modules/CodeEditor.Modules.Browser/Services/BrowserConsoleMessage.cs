namespace CodeEditor.Modules.Browser.Services;

/// <summary>A page console message: level (<c>log</c>, <c>warning</c>, <c>error</c>, <c>exception</c>) and text.</summary>
public sealed record BrowserConsoleMessage(string Level, string Text);
