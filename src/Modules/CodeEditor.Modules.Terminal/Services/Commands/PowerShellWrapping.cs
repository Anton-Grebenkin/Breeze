namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>How much of the launcher's own script surrounds an agent command (<see cref="PowerShellLauncher"/>).</summary>
public enum PowerShellWrapping
{
    /// <summary>UTF-8 prologue and exit code epilogue around the command.</summary>
    Full,

    /// <summary>The command alone: for machines where antivirus blocks the wrapped script.</summary>
    Bare,
}
