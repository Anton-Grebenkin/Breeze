using CodeEditor.Modules.Terminal.Services.Shells;

namespace CodeEditor.Modules.Terminal.Services;

/// <summary>The <c>terminal</c> settings section: the terminal panel and agent commands the user allowed.</summary>
public sealed class TerminalOptions
{
    public const string Section = "terminal";
    public const string DefaultProfileKey = "terminal.defaultProfile";

    /// <summary>The terminal font size in pixels, as in VS Code.</summary>
    public const double DefaultFontSize = 14;

    /// <summary>
    /// Word-wise command prefixes ("dotnet test", "npm install"), written by the card's "Always allow" button and
    /// editable in <c>settings.json</c> (ADR 0012).
    /// </summary>
    public List<string> AllowedCommands { get; set; } = [];

    /// <summary>The shell of a new terminal (<see cref="TerminalProfiles"/> ids); empty — the first one installed.</summary>
    public string? DefaultProfile { get; set; }

    public double FontSize { get; set; } = DefaultFontSize;
}
