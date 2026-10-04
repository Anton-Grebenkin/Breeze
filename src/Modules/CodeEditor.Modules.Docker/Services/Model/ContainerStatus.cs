using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Docker.Services.Cli;

namespace CodeEditor.Modules.Docker.Services.Model;

/// <summary>
/// Container status from <c>docker ps</c>, parsed from docker's English text: "Up 45 hours (healthy)",
/// "Exited (143) 2 months ago", "Restarting (1) 5 seconds ago". Unrecognized text leaves all fields empty, and the panel
/// shows the state without a duration.
/// </summary>
/// <param name="Duration">How long it has been up (Up), or how long ago it stopped (Exited, Restarting).</param>
/// <param name="ExitCode">Exit code of a stopped or restarting container.</param>
public readonly partial record struct ContainerStatus(TimeSpan? Duration, int? ExitCode, ContainerHealth Health)
{
    // A stop by signal is not a failure: Ctrl+C (SIGINT), docker stop (SIGTERM, then SIGKILL on timeout).
    private const int Interrupted = 130;
    private const int Killed = 137;
    private const int Terminated = 143;

    /// <summary>The container failed: the exit code is neither 0 nor a signal stop.</summary>
    public bool IsFailure => ExitCode is { } code and not (0 or Interrupted or Killed or Terminated);

    public static ContainerStatus Parse(string status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (Up().Match(status) is { Success: true } up)
        {
            return new ContainerStatus(DockerTime.ParseDuration(up.Groups["duration"].Value), null, HealthOf(up.Groups["health"].Value));
        }

        if (Stopped().Match(status) is { Success: true } stopped)
        {
            var code = int.Parse(stopped.Groups["code"].ValueSpan, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            return new ContainerStatus(DockerTime.ParseDuration(stopped.Groups["duration"].Value), code, ContainerHealth.None);
        }

        return default;
    }

    private static ContainerHealth HealthOf(string text) => text switch
    {
        "healthy" => ContainerHealth.Healthy,
        "unhealthy" => ContainerHealth.Unhealthy,
        "health: starting" => ContainerHealth.Starting,
        _ => ContainerHealth.None,
    };

    [GeneratedRegex(@"^Up (?<duration>.+?)(?: \((?<health>healthy|unhealthy|health: starting)\))?(?: \(Paused\))?$")]
    private static partial Regex Up();

    [GeneratedRegex(@"^(?:Exited|Restarting) \((?<code>-?\d+)\) (?<duration>.+) ago$")]
    private static partial Regex Stopped();
}
