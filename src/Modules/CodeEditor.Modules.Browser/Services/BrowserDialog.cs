namespace CodeEditor.Modules.Browser.Services;

/// <summary>A dialog a page shows and waits on: <c>alert</c>, <c>confirm</c> or <c>prompt</c>.</summary>
/// <param name="Kind"><c>alert</c>, <c>confirm</c> or <c>prompt</c>.</param>
/// <param name="DefaultText">The text a prompt offers.</param>
public sealed record BrowserDialog(string Kind, string Message, string DefaultText);
