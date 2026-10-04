namespace CodeEditor.Modules.Tools.Commands;

public static class ToolCommandIds
{
    public const string RunTool = "tools.run";
    public const string NewTool = "tools.new";
    public const string Refresh = "tools.refresh";

    /// <summary>Command of one tool from the shelf.</summary>
    public static string Run(string toolName) => RunTool + "." + toolName;
}
