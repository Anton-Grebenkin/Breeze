using CodeEditor.Core.Files;
using CodeEditor.Modules.Docker.Services;
using CodeEditor.Modules.Docker.Services.Cli;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>
/// Panel docker arguments follow the agent's rules: a name never becomes a docker option, the compose file stays inside
/// the folder; logs are the last lines plus a stream. Compose files are looked up in the folder index.
/// </summary>
public sealed class DockerQueriesTests
{
    [Theory]
    [InlineData("-v")]
    [InlineData("--help")]
    [InlineData("api web")]
    [InlineData("")]
    public void UnsafeName_IsRefused(string name) => Assert.Throws<ArgumentException>(() => DockerQueries.InspectContainer(name));

    [Theory]
    [InlineData("../other/compose.yaml")]
    [InlineData(@"..\other\compose.yaml")]
    [InlineData(@"C:\other\compose.yaml")]
    public void ComposeFileOutsideTheFolder_IsRefused(string file) => Assert.Throws<ArgumentException>(() => DockerQueries.ComposeConfig(file));

    [Fact]
    public void Arguments_KeepValuesInOneArgument()
    {
        Assert.Equal(["logs", "--tail=500", "--follow", "api"], DockerQueries.Logs("api", follow: true));
        Assert.Equal(["rm", "--force", "a1"], DockerQueries.RemoveContainer("a1", force: true));
        Assert.Equal(["rm", "a1"], DockerQueries.RemoveContainer("a1", force: false));
        Assert.Equal(["image", "rm", "postgres:17"], DockerQueries.RemoveImage("postgres:17"));
        Assert.Equal(["compose", "--file=deploy/compose.yaml", "restart", "web"], DockerQueries.ComposeRestart("deploy/compose.yaml", "web"));
        Assert.Equal(["compose", "--file=compose.yaml", "config", "--format", "json"], DockerQueries.ComposeConfig("compose.yaml"));
    }

    [Fact]
    public void ComposeFiles_RootFirst_ThenByDepthAndName()
    {
        IndexedFile[] files =
        [
            File("deploy/prod/docker-compose.yml"),
            File("src/App.cs"),
            File("docker-compose.override.yml"),
            File("deploy/compose.yaml"),
            File("compose.yaml"),
            File("my-compose.yml"),
            File("compose.json"),
            File("tools/docker-compose-dev.yaml"),
        ];

        Assert.Equal(
            ["compose.yaml", "docker-compose.override.yml", "deploy/compose.yaml", "tools/docker-compose-dev.yaml", "deploy/prod/docker-compose.yml"],
            ComposeFileFinder.Find(files));
    }

    private static IndexedFile File(string relativePath) =>
        new(Path.Combine(@"C:\app", relativePath), relativePath, Path.GetFileName(relativePath));
}
