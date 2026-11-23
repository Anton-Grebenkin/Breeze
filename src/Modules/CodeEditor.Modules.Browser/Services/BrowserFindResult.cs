namespace CodeEditor.Modules.Browser.Services;

/// <summary>Where "find on page" stands: the match shown (from 1; 0 when there is none) and how many there are.</summary>
public readonly record struct BrowserFindResult(int Active, int Count);
