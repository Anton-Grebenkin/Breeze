using CodeEditor.Modules.Docker.Services.Model;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>Port links in a container row: tcp only and at most <see cref="MaxLinks"/>, since the row is narrow.</summary>
internal static class PortLinks
{
    /// <summary>Other ports appear in the tooltip and in the "Open in Browser" pick.</summary>
    public const int MaxLinks = 3;

    public static IReadOnlyList<PublishedPort> Of(DockerContainer? container) =>
        container is { IsRunning: true } ? [.. container.Ports.Where(port => port.IsTcp).Take(MaxLinks)] : [];

    /// <summary>Keeps the current list when the links match, so the view does not recreate them on refresh.</summary>
    public static IReadOnlyList<PublishedPort> Keep(IReadOnlyList<PublishedPort> current, IReadOnlyList<PublishedPort> next) =>
        current.SequenceEqual(next) ? current : next;
}
