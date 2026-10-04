using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Docker.Services.Agent;

namespace CodeEditor.Modules.Docker.Tests;

/// <summary>
/// docker arguments: short tables, a line limit for logs, option values as one argument, the container command after
/// the image, compose only with an explicit file, a name starting with '-' never becomes an option.
/// </summary>
public sealed class DockerCommandsTests
{
    [Fact]
    public void Ps_ListsAllContainers_InAShortTable() =>
        Assert.Equal(["ps", "--all", "--format", @"table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}"], DockerCommands.Read(new DockerRead("ps")));

    [Fact]
    public void Logs_TailIsLimited_SinceIsOneArgument() =>
        Assert.Equal(["logs", "--tail=2000", "--since=10m", "api"], DockerCommands.Read(new DockerRead("logs", "api", Tail: 50_000, Since: "10m")));

    [Fact]
    public void ComposeLogs_GoWithTheFileAndServices() =>
        Assert.Equal(["compose", "--file=compose.yaml", "logs", "--no-color", "--tail=200", "api", "db"],
            DockerCommands.Read(new DockerRead("compose_logs") { ComposeFile = "compose.yaml", Services = ["api", "db"] }));

    // A model value never becomes a docker option.
    [Theory]
    [InlineData("--privileged")]
    [InlineData("my container")]
    [InlineData("")]
    public void ContainerName_CannotBeAKey(string name) =>
        Assert.Throws<AgentToolException>(() => DockerCommands.Change(new DockerChange("stop") { Name = name }));

    [Fact]
    public void Run_OptionsAreSingleArguments_CommandGoesAfterTheImage()
    {
        var arguments = DockerCommands.Change(new DockerChange("run")
        {
            Name = "web",
            Image = "nginx:1.27",
            Ports = ["8080:80"],
            Env = ["GREETING=hello world", "MODE=-x"],
            Volumes = ["data:/data"],
            Network = "dev",
            Command = ["--help"],
        });

        Assert.Equal(
            ["run", "--detach", "--name=web", "--publish=8080:80", "--env=GREETING=hello world", "--env=MODE=-x", "--volume=data:/data", "--network=dev", "nginx:1.27", "--help"],
            arguments);
    }

    [Fact]
    public void Run_InTheForeground_RemovesTheContainer() =>
        Assert.Equal(["run", "--rm", "alpine", "echo", "hi"], DockerCommands.Change(new DockerChange("run") { Image = "alpine", Command = ["echo", "hi"], Detach = false }));

    // A variable without a value would take it from the editor's environment.
    [Fact]
    public void Run_VariableWithoutValue_IsRefused() =>
        Assert.Throws<AgentToolException>(() => DockerCommands.Change(new DockerChange("run") { Image = "alpine", Env = ["OPENAI_API_KEY"] }));

    [Fact]
    public void Exec_NeedsACommand() =>
        Assert.Throws<AgentToolException>(() => DockerCommands.Change(new DockerChange("exec") { Name = "api" }));

    [Fact]
    public void Build_TagFileAndFolder()
    {
        Assert.Equal(["build", "--tag=app:dev", "--file=src/Api/Dockerfile", "src"],
            DockerCommands.Change(new DockerChange("build") { Image = "app:dev", Dockerfile = "src/Api/Dockerfile", Context = "src" }));
        Assert.Equal(["build", "."], DockerCommands.Change(new DockerChange("build")));
        Assert.Equal(["build", "./-odd"], DockerCommands.Change(new DockerChange("build") { Context = "-odd" }));
    }

    [Fact]
    public void ComposeUp_InTheBackground_WithBuild() =>
        Assert.Equal(["compose", "--file=compose.yaml", "up", "--detach", "--build", "api"],
            DockerCommands.Change(new DockerChange("compose_up") { ComposeFile = "compose.yaml", Build = true, Services = ["api"] }));

    // Without a file docker would search parent folders too and could stop someone else's project.
    [Fact]
    public void Compose_WithoutAFile_IsRefused() =>
        Assert.Throws<AgentToolException>(() => DockerCommands.Change(new DockerChange("compose_down")));

    [Fact]
    public void UnknownAction_ListsTheActions()
    {
        var error = Assert.Throws<AgentToolException>(() => DockerCommands.Read(new DockerRead("prune")));

        Assert.Contains("compose_logs", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Display_QuotesArgumentsWithSpaces() =>
        Assert.Equal("docker run --detach \"--env=GREETING=hello world\" alpine", DockerCommands.Display(["run", "--detach", "--env=GREETING=hello world", "alpine"]));
}
