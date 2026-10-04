using CodeEditor.Modules.Agent.Services.Context;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Agent.Tests.Context;

/// <summary>Command-line tools in the folder snapshot: what is on PATH and what is missing.</summary>
public sealed class EnvironmentToolsTests
{
    private static readonly string Tools = Path.GetFullPath(@"C:\tools");
    private static readonly string Node = Path.GetFullPath(@"C:\node");
    private static readonly string Store = Path.GetFullPath(@"C:\Users\u\AppData\Local\Microsoft\WindowsApps");

    // The Microsoft Store python alias opens the Store, not Python (models kept trying to run it).
    [Fact]
    public async Task ListsFoundTools_AndMissing_StorePythonIsMissing()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(Path.Combine(Tools, "git.exe"), string.Empty)
            .AddFile(Path.Combine(Node, "node.exe"), string.Empty)
            .AddFile(Path.Combine(Node, "npm.cmd"), string.Empty)
            .AddFile(Path.Combine(Store, "python.exe"), string.Empty);
        var tools = new EnvironmentTools(fileSystem, $"{Tools};\"{Node}\";;{Store}", ".EXE;.CMD");

        Assert.Equal("Tools on PATH: git, node, npm; not found: dotnet, python, pwsh, java, docker.", await tools.DescribeAsync());
    }

    [Fact]
    public async Task EmptyPath_SaysNothing() =>
        Assert.Null(await new EnvironmentTools(new FakeFileSystem(), string.Empty).DescribeAsync());
}
