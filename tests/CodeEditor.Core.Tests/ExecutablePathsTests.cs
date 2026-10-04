using CodeEditor.Core.Processes;
using CodeEditor.Testing;

namespace CodeEditor.Core.Tests;

/// <summary>Finding a program on PATH: folder order, PATHEXT extensions, quotes and empty entries.</summary>
public sealed class ExecutablePathsTests
{
    private static readonly string First = Path.GetFullPath(@"C:\first");
    private static readonly string Second = Path.GetFullPath(@"C:\second tools");

    private readonly FakeFileSystem _files = new FakeFileSystem()
        .AddFile(Path.Combine(First, "docker.cmd"))
        .AddFile(Path.Combine(Second, "docker.exe"));

    [Fact]
    public void Find_ReturnsMatchesInPathOrder()
    {
        var found = ExecutablePaths.Find(_files, "docker", $"{First};;\"{Second}\"", ".EXE;.CMD").ToList();

        Assert.Equal([Path.Combine(First, "docker.cmd"), Path.Combine(Second, "docker.exe")], found);
    }

    [Fact]
    public void Find_WithoutTheProgram_IsEmpty() =>
        Assert.Empty(ExecutablePaths.Find(_files, "git", $"{First};{Second}", ".EXE;.CMD"));
}
