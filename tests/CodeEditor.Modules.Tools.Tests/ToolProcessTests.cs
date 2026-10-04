using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Tools.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Tools.Tests;

/// <summary>A real PowerShell script: parameters and variables reach it the way the template says.</summary>
public sealed class ToolProcessTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("breeze-tools-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task PowerShellScript_GetsNamedParametersAndVariables()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, ".breeze", "tools", "echo")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "tool.md"), "---\ndescription: Echo\nparameters:\n  path: p\n  note: n\n---\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(folder, "run.ps1"), "param([string]$path = 'none', [string]$note = 'default')\n\"$path|$note|$env:BREEZE_ARG_PATH|$((Get-Location).Path)\"\n", TestContext.Current.CancellationToken);
        var fileSystem = new PhysicalFileSystem();
        using var workspace = new Workspace(fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        workspace.Open(_root);
        using var shelf = new ToolShelf(workspace, fileSystem, new UserDataPaths(Path.Combine(_root, "userdata")), NullLogger<ToolShelf>.Instance);
        var runner = new ToolRunner(new ProcessRunner(), fileSystem, workspace);

        var request = runner.Request(shelf.Find("echo")!, new Dictionary<string, string> { ["path"] = "src dir", ["note"] = string.Empty }, forAgent: true);
        var result = await runner.RunAsync(request, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"src dir|default|src dir|{_root}", result.Output.Trim());
    }
}
