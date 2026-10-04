using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Terminal.Resources;

namespace CodeEditor.Modules.Terminal.Services.Shells;

/// <summary>
/// The shells installed on this machine, in order of preference: PowerShell 7, Windows PowerShell, the command
/// prompt, Git Bash. A new terminal starts the one chosen in <c>terminal.defaultProfile</c>, else the first found.
/// </summary>
public sealed class TerminalProfiles(IFileSystem fileSystem, ShellLocations locations)
{
    public const string PowerShell = "pwsh";
    public const string WindowsPowerShell = "powershell";
    public const string CommandPrompt = "cmd";
    public const string GitBash = "gitbash";

    private const string NoLogo = "-NoLogo";
    private const string ExecutableExtension = ".EXE";

    /// <remarks>A few file checks each call: the list follows installs without a restart.</remarks>
    public IReadOnlyList<TerminalProfile> Available() =>
    [
        .. Profile(PowerShell, Strings.ProfilePowerShell, FindPowerShell(), NoLogo),
        .. Profile(WindowsPowerShell, Strings.ProfileWindowsPowerShell, Existing(Path.Combine(locations.SystemFolder, "WindowsPowerShell", "v1.0", "powershell.exe")), NoLogo),
        .. Profile(CommandPrompt, Strings.ProfileCommandPrompt, Existing(locations.CommandInterpreter) ?? Existing(Path.Combine(locations.SystemFolder, "cmd.exe"))),
        .. Profile(GitBash, Strings.ProfileGitBash, Existing(Path.Combine(locations.ProgramFiles, "Git", "bin", "bash.exe")), "--login -i"),
    ];

    /// <summary>The preferred shell if installed, else the first available; <c>null</c> when none is found.</summary>
    public TerminalProfile? Default(string? preferred)
    {
        var available = Available();
        return available.FirstOrDefault(profile => string.Equals(profile.Id, preferred, StringComparison.OrdinalIgnoreCase))
            ?? available.FirstOrDefault();
    }

    private string? FindPowerShell() =>
        ExecutablePaths.Find(fileSystem, PowerShell, locations.Path, ExecutableExtension).FirstOrDefault()
        ?? Existing(Path.Combine(locations.ProgramFiles, "PowerShell", "7", "pwsh.exe"));

    private string? Existing(string? path) => path is not null && fileSystem.FileExists(path) ? path : null;

    private static TerminalProfile[] Profile(string id, string name, string? executable, string arguments = "") =>
        executable is null ? [] : [new TerminalProfile(id, name, executable, arguments)];
}
