using CodeEditor.Core.Files;
using CodeEditor.Core.Output;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>A folder file index set up by the test.</summary>
internal sealed class StaticFileIndex : IFileIndex
{
    public IReadOnlyList<IndexedFile> Files { get; set; } = [];

    public Task WhenReady => Task.CompletedTask;

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public void Add(string root, string relativePath) =>
        Files = [.. Files, new IndexedFile(Path.Combine(root, relativePath.Replace('/', '\\')), relativePath, Path.GetFileName(relativePath))];
}

/// <summary>In-memory output channels the test can read.</summary>
internal sealed class FakeOutput : IOutputService
{
    private readonly Dictionary<string, FakeChannel> _channels = new(StringComparer.Ordinal);

    public event EventHandler? ChannelsChanged
    {
        add { }
        remove { }
    }

    public IReadOnlyList<IOutputChannel> Channels => [.. _channels.Values];

    public IOutputChannel GetOrCreate(string name)
    {
        lock (_channels)
        {
            if (!_channels.TryGetValue(name, out var channel))
            {
                _channels[name] = channel = new FakeChannel(name);
            }

            return channel;
        }
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

        public void Clear()
        {
            lock (_lines)
            {
                _lines.Clear();
            }
        }

        public string Snapshot()
        {
            lock (_lines)
            {
                return string.Join('\n', _lines);
            }
        }
    }
}

/// <summary>View tabs without an editor area: records requests and reuses the tab with the same id.</summary>
internal sealed class RecordingEditorViews : IEditorViews
{
    private readonly Dictionary<string, EditorTab> _open = new(StringComparer.Ordinal);

    public List<EditorViewRequest> Requests { get; } = [];

    public EditorTab Open(EditorViewRequest request)
    {
        Requests.Add(request);
        if (!_open.TryGetValue(request.Id, out var tab))
        {
            _open[request.Id] = tab = new ViewTabViewModel(request.Id, request.Title, request.ToolTip, filePath: null, request.CreateContent(), request.Preview);
        }

        return tab;
    }

    public void Close(string id)
    {
        if (_open.Remove(id, out var tab))
        {
            tab.Dispose();
        }
    }

    public object Content(string id) => _open[id].Editor;
}
