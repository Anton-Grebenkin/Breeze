namespace CodeEditor.Modules.Browser.Services;

/// <summary>An element the user picked on a page with the mouse, to show the agent what they mean.</summary>
/// <param name="Tag">The tag name, lower case.</param>
/// <param name="Text">The element's visible text, cut short.</param>
/// <param name="Selector">A CSS selector that finds the element from the nearest id.</param>
/// <param name="Html">The start of the element's markup.</param>
public sealed record BrowserPickedElement(string Tag, string Text, string Selector, string Html);
