using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.Services.Model;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>
/// Node texts in the UI language: container state ("running 45 h", "stopped 2 wk ago"), image size ("113 MB"), tooltips.
/// docker writes them in English; the panel translates what it parses and shows the rest as is.
/// </summary>
internal static partial class DockerNodeText
{
    public const string Separator = " · ";

    public static string State(DockerContainer container)
    {
        var details = container.Details;
        var since = details.Duration is { } duration ? DockerTime.Format(duration) : null;
        return container.State switch
        {
            ContainerState.Running => since is null ? Strings.StateRunning : Format(Strings.StateRunningFor, since),
            ContainerState.Paused => Strings.StatePaused,
            ContainerState.Restarting => Strings.StateRestarting,
            ContainerState.Created => Strings.StateCreated,
            ContainerState.Removing => Strings.StateRemoving,
            ContainerState.Dead => Strings.StateDead,
            ContainerState.Exited when details.IsFailure =>
                since is null ? Format(Strings.StateFailed, details.ExitCode!) : Format(Strings.StateFailedAgo, since, details.ExitCode!),
            ContainerState.Exited => since is null ? Strings.StateExited : Format(Strings.StateExitedAgo, since),
            _ => container.Status,
        };
    }

    /// <summary>"running 45 h · unhealthy · postgres:17".</summary>
    public static string Container(DockerContainer container) => Join(State(container), Health(container), container.Image);

    public static string ContainerToolTip(DockerContainer container) => Lines(
        container.Name,
        Format(Strings.ToolTipImage, container.Image),
        Format(Strings.ToolTipState, Join(State(container), Health(container))),
        container.Ports.Count > 0 ? Format(Strings.ToolTipPorts, string.Join(", ", container.Ports.Select(port => port.Mapping))) : null,
        container.Project is { } project ? Format(Strings.ToolTipCompose, project, container.Service ?? string.Empty) : null,
        Format(Strings.ToolTipId, container.ShortId));

    /// <summary>A compose service: its container's state, or "not started".</summary>
    public static string Service(DockerContainer? container) =>
        container is null ? Strings.ServiceNotStarted : Join(State(container), Health(container));

    /// <summary>"3 of 4 running".</summary>
    public static string Running(int running, int total) =>
        Format(Strings.RunningSummary, running, total, Plural.Select(running, Strings.RunningForms));

    /// <summary>"113 MB · 2 mo".</summary>
    public static string Image(DockerImage image, DateTimeOffset now) =>
        Join(Size(image.Size), image.Created is { } created ? DockerTime.Format(now - created) : null);

    public static string ImageToolTip(DockerImage image) => Lines(
        image.Title,
        Format(Strings.ToolTipId, image.Id),
        Format(Strings.ToolTipSize, Size(image.Size)),
        image.Created is { } created ? Format(Strings.ToolTipCreated, created.ToLocalTime().ToString("g", CultureInfo.CurrentUICulture)) : null);

    /// <summary>docker size "9.73MB" in the UI language's number format and unit; unparsed sizes stay as is.</summary>
    public static string Size(string size)
    {
        var match = DockerSize().Match(size);
        if (!match.Success)
        {
            return size;
        }

        var value = double.Parse(match.Groups["value"].ValueSpan, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value.ToUpperInvariant() switch
        {
            "B" => Strings.SizeBytes,
            "KB" => Strings.SizeKilobytes,
            "MB" => Strings.SizeMegabytes,
            "GB" => Strings.SizeGigabytes,
            _ => Strings.SizeTerabytes,
        };
        // Decimal separator of the UI language.
        return Format(unit, value.ToString("0.##", CultureInfo.CurrentUICulture));
    }

    public static string Join(params string?[] parts) => string.Join(Separator, parts.Where(part => !string.IsNullOrEmpty(part)));

    public static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

    private static string? Health(DockerContainer container) => container.Details.Health switch
    {
        ContainerHealth.Unhealthy => Strings.HealthUnhealthy,
        ContainerHealth.Starting => Strings.HealthStarting,
        _ => null,
    };

    private static string Lines(params string?[] lines) => string.Join('\n', lines.Where(line => !string.IsNullOrEmpty(line)));

    [GeneratedRegex(@"^(?<value>\d+(?:\.\d+)?)\s?(?<unit>B|kB|KB|MB|GB|TB)$")]
    private static partial Regex DockerSize();
}
