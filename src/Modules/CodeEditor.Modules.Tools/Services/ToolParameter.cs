namespace CodeEditor.Modules.Tools.Services;

/// <summary>Named input of a tool: the script gets it as <c>-name value</c> (PowerShell) or <c>--name value</c>.</summary>
public readonly record struct ToolParameter(string Name, string Description);
