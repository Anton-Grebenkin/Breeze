namespace CodeEditor.Modules.Terminal.Services.Pty;

/// <summary>What to start in a pseudo console and its initial size in character cells.</summary>
public sealed record TerminalLaunch(string CommandLine, string WorkingDirectory, int Columns, int Rows);
