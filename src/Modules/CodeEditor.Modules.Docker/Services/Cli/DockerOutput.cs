using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Docker.Services.Model;

namespace CodeEditor.Modules.Docker.Services.Cli;

/// <summary>
/// Parses docker output for the panel: line-delimited JSON of containers and images (<see cref="DockerQueries"/>), ports
/// ("0.0.0.0:8080->80/tcp, [::]:8080->80/tcp") and the compose project. A line that does not parse is skipped and the
/// panel shows the rest. One pass over the output, O(output length).
/// </summary>
public static class DockerOutput
{
    /// <summary>A port range like "9000-9100" expands at most this far: a row needs no hundreds of links.</summary>
    public const int MaxPortRange = 16;

    private const string Arrow = "->";

    public static IReadOnlyList<DockerContainer> Containers(string output) => JsonLines(output, Container);

    public static IReadOnlyList<DockerImage> Images(string output) => JsonLines(output, Image);

    /// <summary>Published ports; IPv4 and IPv6 of one port count once, an entry without "->" is skipped.</summary>
    public static IReadOnlyList<PublishedPort> Ports(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ports = new List<PublishedPort>();
        var seen = new HashSet<(int, string)>();
        foreach (var entry in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var port in Expand(entry).Where(port => seen.Add((port.HostPort, port.Protocol))))
            {
                ports.Add(port);
            }
        }

        return ports;
    }

    /// <summary>
    /// The project from <c>docker compose config --format json</c>: name and services in alphabetical order. compose
    /// warnings ("version is obsolete") arrive in the same output as separate lines, so only lines of the indented JSON
    /// (starting with a brace or whitespace) are kept.
    /// </summary>
    public static ComposeProject Compose(string file, string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var json = string.Join('\n', output.Split('\n').Where(line => line.Length > 0 && (char.IsWhiteSpace(line[0]) || line[0] is '{' or '}')));
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var services = root.TryGetProperty("services", out var map) && map.ValueKind == JsonValueKind.Object
                ? map.EnumerateObject().Select(service => service.Name).Order(NaturalStringComparer.Instance).ToList()
                : [];
            return new ComposeProject(file, Text(root, "name"), services);
        }
        catch (JsonException exception)
        {
            return ComposeProject.Failed(file, exception.Message);
        }
    }

    // Each line is its own document; items are mapped before it is disposed, so elements need no Clone().
    private static List<T> JsonLines<T>(string output, Func<JsonElement, T?> map)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(output);
        var items = new List<T>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith('{'))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (map(document.RootElement) is { } item)
                {
                    items.Add(item);
                }
            }
            catch (JsonException)
            {
                // A line that does not parse is skipped.
            }
        }

        return items;
    }

    private static DockerContainer? Container(JsonElement line) =>
        Text(line, "id") is { Length: > 0 } id && Text(line, "name") is { Length: > 0 } name
            ? new DockerContainer(id, name, Text(line, "image") ?? string.Empty, State(Text(line, "state")), Text(line, "status") ?? string.Empty)
            {
                Ports = Ports(Text(line, "ports") ?? string.Empty),
                Project = Text(line, "project") is { Length: > 0 } project ? project : null,
                Service = Text(line, "service") is { Length: > 0 } service ? service : null,
            }
            : null;

    private static DockerImage? Image(JsonElement line) =>
        Text(line, "id") is { Length: > 0 } id
            ? new DockerImage(id, Text(line, "repository") ?? string.Empty, Text(line, "tag") ?? string.Empty, Text(line, "size") ?? string.Empty,
                DockerTime.ParseTimestamp(Text(line, "created") ?? string.Empty))
            : null;

    private static ContainerState State(string? state) =>
        Enum.TryParse<ContainerState>(state, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : ContainerState.Unknown;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // "0.0.0.0:9000-9001->9000-9001/tcp" gives 9000→9000 and 9001→9001; "9092/tcp" is not published.
    private static IEnumerable<PublishedPort> Expand(string entry)
    {
        var arrow = entry.IndexOf(Arrow, StringComparison.Ordinal);
        if (arrow < 0)
        {
            yield break;
        }

        var source = entry[..arrow];
        var target = entry[(arrow + Arrow.Length)..];
        var colon = source.LastIndexOf(':');
        var slash = target.IndexOf('/');
        var protocol = slash >= 0 ? target[(slash + 1)..] : "tcp";
        if (Range(source[(colon + 1)..]) is not { } hostPorts || Range(slash >= 0 ? target[..slash] : target) is not { } containerPorts)
        {
            yield break;
        }

        var host = colon > 0 ? source[..colon] : string.Empty;
        for (var offset = 0; offset <= hostPorts.Last - hostPorts.First && offset < MaxPortRange; offset++)
        {
            yield return new PublishedPort(host, hostPorts.First + offset, containerPorts.First + offset, protocol);
        }
    }

    private static (int First, int Last)? Range(string text)
    {
        var dash = text.IndexOf('-', StringComparison.Ordinal);
        var firstText = dash >= 0 ? text[..dash] : text;
        var lastText = dash >= 0 ? text[(dash + 1)..] : text;
        return int.TryParse(firstText, NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            && int.TryParse(lastText, NumberStyles.None, CultureInfo.InvariantCulture, out var last) && last >= first
                ? (first, last)
                : null;
    }
}
