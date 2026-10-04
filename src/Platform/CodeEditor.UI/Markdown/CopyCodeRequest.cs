namespace CodeEditor.UI.Markdown;

/// <summary>Tag of a code block's Copy link: the viewer puts the code on the clipboard.</summary>
internal sealed record CopyCodeRequest(string Code);
