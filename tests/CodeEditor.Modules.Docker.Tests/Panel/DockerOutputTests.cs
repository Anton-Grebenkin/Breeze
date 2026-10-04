using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.Services.Model;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>
/// Parsing docker output for the panel: JSON lines of containers and images (non-JSON lines are skipped), ports without
/// IPv4/IPv6 duplicates and with expanded ranges, the compose project among compose warnings.
/// </summary>
public sealed class DockerOutputTests
{
    [Fact]
    public void Containers_AreReadFromJsonLines_SkippingOtherLines()
    {
        var output = string.Join('\n',
            "WARNING: some CLI notice",
            PanelFixture.Container("a1b2c3d4e5f6a7b8", "app-api-1", "app-api", "running", "Up 2 hours (healthy)", "0.0.0.0:8080->80/tcp, [::]:8080->80/tcp", "app", "api"),
            "{not json",
            PanelFixture.Container("c1", "cache", "redis:7", "exited", "Exited (0) 3 days ago"));

        var containers = DockerOutput.Containers(output);

        Assert.Equal(2, containers.Count);
        var api = containers[0];
        Assert.Equal(("app-api-1", "app-api", ContainerState.Running, "app", "api"), (api.Name, api.Image, api.State, api.Project, api.Service));
        Assert.Equal("a1b2c3d4e5f6", api.ShortId);
        Assert.Equal((TimeSpan.FromHours(2), ContainerHealth.Healthy), (api.Details.Duration, api.Details.Health));
        Assert.Equal(8080, Assert.Single(api.Ports).HostPort);
        Assert.Equal((ContainerState.Exited, null, false), (containers[1].State, containers[1].Project, containers[1].IsRunning));
    }

    [Fact]
    public void Ports_DropUnpublishedAndDuplicates_ExpandRanges()
    {
        var ports = DockerOutput.Ports("9092/tcp, 0.0.0.0:29092->29092/tcp, [::]:29092->29092/tcp, 0.0.0.0:9000-9001->9000-9001/tcp, 127.0.0.1:5353->53/udp");

        Assert.Equal([29092, 9000, 9001, 5353], ports.Select(port => port.HostPort));
        Assert.Equal("http://localhost:29092/", ports[0].Url.AbsoluteUri);
        Assert.Equal((9001, 9001), (ports[2].HostPort, ports[2].ContainerPort));
        Assert.False(ports[3].IsTcp);
        Assert.Equal("http://127.0.0.1:5353/", ports[3].Url.AbsoluteUri);
    }

    [Fact]
    public void Port443_OpensOverHttps() =>
        Assert.Equal("https://localhost/", Assert.Single(DockerOutput.Ports("0.0.0.0:443->8443/tcp")).Url.AbsoluteUri);

    [Fact]
    public void Images_ReadNameSizeAndCreationTime_DanglingByIdentifier()
    {
        var images = DockerOutput.Images(string.Join('\n', PanelFixture.Image("bcb827524ebc", "redis", "7"), PanelFixture.Image("5ed9e5f9a62f", "<none>", "<none>")));

        Assert.Equal(("redis:7", "redis:7", false), (images[0].Title, images[0].Reference, images[0].IsDangling));
        Assert.Equal(new DateTimeOffset(2026, 7, 14, 8, 38, 19, TimeSpan.FromHours(7)), images[0].Created);
        Assert.Equal(("<none>", "5ed9e5f9a62f", true), (images[1].Title, images[1].Reference, images[1].IsDangling));
    }

    // compose warnings arrive in the same output as the JSON.
    [Fact]
    public void Compose_ReadsProjectNameAndServices_AmongWarnings()
    {
        const string Output = """
            time="2026-10-03T10:00:00+07:00" level=warning msg="the attribute `version` is obsolete"
            {
              "name": "shop",
              "services": {
                "web": { "image": "nginx" },
                "db": { "environment": { "POSTGRES_PASSWORD": "secret" } }
              }
            }
            """;

        var project = DockerOutput.Compose("deploy/compose.yaml", Output);

        Assert.Equal(("deploy/compose.yaml", "shop", null), (project.File, project.Name, project.Error));
        Assert.Equal(["db", "web"], project.Services);
    }

    [Fact]
    public void Compose_NotJson_IsAReadError() =>
        Assert.NotNull(DockerOutput.Compose("compose.yaml", "service \"web\" has neither an image nor a build context").Error);
}
