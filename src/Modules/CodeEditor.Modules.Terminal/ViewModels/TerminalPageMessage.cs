namespace CodeEditor.Modules.Terminal.ViewModels;

/// <summary>A message from the terminal page: <c>ready</c>, <c>input</c>, <c>resize</c>, <c>copy</c> or <c>paste</c>.</summary>
public sealed record TerminalPageMessage(string Type, int Id = 0, string? Data = null, int Columns = 0, int Rows = 0);
