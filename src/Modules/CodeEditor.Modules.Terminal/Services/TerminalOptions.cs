namespace CodeEditor.Modules.Terminal.Services;

/// <summary>The <c>terminal</c> settings section: agent commands the user allowed to run without asking.</summary>
public sealed class TerminalOptions
{
    public const string Section = "terminal";

    /// <summary>
    /// Word-wise command prefixes ("dotnet test", "npm install"), written by the card's "Always allow" button and
    /// editable in <c>settings.json</c> (ADR 0012).
    /// </summary>
    public List<string> AllowedCommands { get; set; } = [];
}
