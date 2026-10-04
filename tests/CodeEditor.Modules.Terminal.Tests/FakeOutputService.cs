using CodeEditor.Core.Output;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>In-memory output channels.</summary>
internal sealed class FakeOutputService : IOutputService
{
    private readonly Dictionary<string, Channel> _channels = [];

    public event EventHandler? ChannelsChanged;

    public IReadOnlyList<IOutputChannel> Channels => [.. _channels.Values];

    public IOutputChannel GetOrCreate(string name)
    {
        if (!_channels.TryGetValue(name, out var channel))
        {
            _channels[name] = channel = new Channel(name);
            ChannelsChanged?.Invoke(this, EventArgs.Empty);
        }

        return channel;
    }

    private sealed class Channel(string name) : IOutputChannel
    {
        private readonly List<string> _lines = [];

        public string Name => name;

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
                Version++;
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
