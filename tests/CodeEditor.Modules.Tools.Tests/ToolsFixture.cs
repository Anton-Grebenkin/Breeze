using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Output;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Tools.Services;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Tools.Tests;

/// <summary>A folder with a tool shelf on a fake file system; pwsh, node and dotnet "installed" in C:\bin.</summary>
internal sealed class ToolsFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\repo");
    public static readonly string UserData = Path.GetFullPath(@"C:\userdata");
    public static readonly string Bin = Path.GetFullPath(@"C:\bin");

    public ToolsFixture()
    {
        FileSystem.AddDirectory(Root).AddDirectory(UserData).AddDirectory(Bin);
        foreach (var program in new[] { "pwsh.exe", "node.exe", "dotnet.exe" })
        {
            FileSystem.AddFile(Path.Combine(Bin, program));
        }

        Workspace = new Workspace(FileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        var paths = new UserDataPaths(UserData);
        Shelf = new ToolShelf(Workspace, FileSystem, paths, NullLogger<ToolShelf>.Instance);
        Trust = new ToolTrust(FileSystem, paths, NullLogger<ToolTrust>.Instance);
        Runner = new ToolRunner(Processes, FileSystem, Workspace) { SearchPath = Bin };
        Activity = new ToolActivity(Output, StatusBar, new InlineUiDispatcher());
        AgentTools = new ToolAgentTools(Shelf, Runner, Trust, Activity, Workspace, FileSystem, new PassThroughOutputStore());
        Workspace.Open(Root);
    }

    public FakeFileSystem FileSystem { get; } = new();

    public Workspace Workspace { get; }

    public ToolShelf Shelf { get; }

    public ToolTrust Trust { get; }

    public ToolRunner Runner { get; }

    public FakeProcessRunner Processes { get; } = new();

    public FakeOutput Output { get; } = new();

    public StatusBarViewModel StatusBar { get; } = new();

    public ToolActivity Activity { get; }

    public ToolAgentTools AgentTools { get; }

    /// <summary>Adds a tool to the folder shelf (or the personal one) and rescans.</summary>
    public string AddTool(string name, string definition, string script = "run.ps1", string code = "Write-Output ok", bool personal = false)
    {
        var shelf = personal ? Path.Combine(UserData, "tools") : Path.Combine(Root, ".breeze", "tools");
        var folder = Path.Combine(shelf, name);
        FileSystem.AddDirectory(folder);
        FileSystem.AddFile(Path.Combine(folder, ToolDefinition.DefinitionFile), definition);
        if (script.Length > 0)
        {
            FileSystem.AddFile(Path.Combine(folder, script), code);
        }

        Shelf.Refresh();
        return folder;
    }

    public void Dispose()
    {
        Shelf.Dispose();
        Workspace.Dispose();
    }

    private sealed class PassThroughOutputStore : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }
}

/// <summary>Output channels in memory.</summary>
internal sealed class FakeOutput : IOutputService
{
    private readonly Dictionary<string, FakeChannel> _channels = new(StringComparer.Ordinal);

    public event EventHandler? ChannelsChanged
    {
        add { }
        remove { }
    }

    public IReadOnlyList<IOutputChannel> Channels => [.. _channels.Values];

    public string Text(string name) => _channels.TryGetValue(name, out var channel) ? channel.Snapshot() : string.Empty;

    public IOutputChannel GetOrCreate(string name)
    {
        if (!_channels.TryGetValue(name, out var channel))
        {
            _channels[name] = channel = new FakeChannel(name);
        }

        return channel;
    }

    private sealed class FakeChannel(string name) : IOutputChannel
    {
        private readonly List<string> _lines = [];

        public string Name { get; } = name;

        public long Version { get; private set; }

        public void AppendLine(string text)
        {
            lock (_lines)
            {
                _lines.Add(text);
                Version++;
            }
        }

        public void Clear() => _lines.Clear();

        public string Snapshot()
        {
            lock (_lines)
            {
                return string.Join('\n', _lines);
            }
        }
    }
}
