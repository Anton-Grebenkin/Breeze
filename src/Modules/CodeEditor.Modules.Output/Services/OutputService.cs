using CodeEditor.Core.Output;

namespace CodeEditor.Modules.Output.Services;

/// <summary>
/// Output channels by name. Thread-safe: channels can be created and written from background tasks.
/// </summary>
public sealed class OutputService : IOutputService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, OutputChannel> _byName = new(StringComparer.Ordinal);
    private IReadOnlyList<IOutputChannel> _channels = [];

    public event EventHandler? ChannelsChanged;

    public IReadOnlyList<IOutputChannel> Channels => Volatile.Read(ref _channels);

    public IOutputChannel GetOrCreate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        OutputChannel channel;
        lock (_lock)
        {
            if (_byName.TryGetValue(name, out var existing))
            {
                return existing;
            }

            channel = new OutputChannel(name);
            _byName.Add(name, channel);
            Volatile.Write(ref _channels, [.. _channels, channel]);
        }

        ChannelsChanged?.Invoke(this, EventArgs.Empty);
        return channel;
    }
}
