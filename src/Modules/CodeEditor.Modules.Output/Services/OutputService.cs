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

    /// <summary>A channel asked to be shown (<see cref="Reveal"/>); raised on the caller's thread.</summary>
    public event EventHandler<IOutputChannel>? RevealRequested;

    public IReadOnlyList<IOutputChannel> Channels => Volatile.Read(ref _channels);

    public void Reveal(IOutputChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        RevealRequested?.Invoke(this, channel);
    }

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
