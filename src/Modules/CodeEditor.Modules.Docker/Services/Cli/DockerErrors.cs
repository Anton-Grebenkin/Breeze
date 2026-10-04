using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Docker.Services.Cli;

/// <summary>
/// docker errors for the user. "Engine not running" is recognized by the client's text ("error during connect …
/// dockerDesktopLinuxEngine" from Docker Desktop on Windows, "Cannot connect to the Docker daemon" on Linux); anything
/// else is shown as the first output line, since the rest is usually debugging detail.
/// </summary>
public static partial class DockerErrors
{
    /// <summary>Length limit: the error goes to the status bar and the panel.</summary>
    public const int MaxLength = 300;

    public static bool IsEngineStopped(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return EngineStopped().IsMatch(output);
    }

    /// <summary>The first non-empty output line, at most <see cref="MaxLength"/> characters.</summary>
    public static string FirstLine(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        return line.Length > MaxLength ? line[..MaxLength] + "…" : line;
    }

    [GeneratedRegex(
        "error during connect|cannot connect to the docker daemon|failed to connect to the docker api|docker daemon is not running|dockerDesktopLinuxEngine",
        RegexOptions.IgnoreCase)]
    private static partial Regex EngineStopped();
}
