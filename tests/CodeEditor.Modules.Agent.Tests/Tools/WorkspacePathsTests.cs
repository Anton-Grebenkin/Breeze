using System.Diagnostics;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Tools;

/// <summary>
/// The workspace boundary on a real disk: a junction inside the folder may point anywhere, so the boundary is where the
/// path really leads, not how it is spelled.
/// </summary>
public sealed class WorkspacePathsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("breeze-root-").FullName;
    private readonly string _outside = Directory.CreateTempSubdirectory("breeze-outside-").FullName;
    private readonly List<string> _junctions = [];

    public void Dispose()
    {
        foreach (var junction in _junctions.Where(Directory.Exists))
        {
            Directory.Delete(junction);
        }

        Directory.Delete(_root, recursive: true);
        Directory.Delete(_outside, recursive: true);
    }

    [Fact]
    public void JunctionLeadingOutside_IsOutsideTheWorkspace_OneLeadingInsideIsNot()
    {
        File.WriteAllText(Path.Combine(_outside, "secret.txt"), "x");
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        Junction("vendor", _outside);
        Junction("alias", Path.Combine(_root, "src"));
        using var workspace = new Workspace(new FakeFileSystem().AddDirectory(_root), new ContextKeyService(), NullLogger<Workspace>.Instance);
        workspace.Open(_root);

        Assert.Throws<AgentToolException>(() => WorkspacePaths.Resolve(workspace, "vendor/secret.txt"));
        Assert.Throws<AgentToolException>(() => WorkspacePaths.Resolve(workspace, "vendor/new/file.txt"));
        Assert.Throws<AgentToolException>(() => WorkspacePaths.Resolve(workspace, "vendor"));
        Assert.Equal(Path.Combine(_root, "alias", "a.cs"), WorkspacePaths.Resolve(workspace, "alias/a.cs"));
        Assert.Equal(Path.Combine(_root, "src", "new", "a.cs"), WorkspacePaths.Resolve(workspace, "src/new/a.cs"));
    }

    // A junction needs no privilege, unlike a symbolic link.
    private void Junction(string name, string target)
    {
        var link = Path.Combine(_root, name);
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target]) { CreateNoWindow = true, RedirectStandardOutput = true })!;
        process.WaitForExit();
        if (process.ExitCode != 0 || !Directory.Exists(link))
        {
            Assert.Skip("Junctions cannot be created here.");
        }

        _junctions.Add(link);
    }
}
