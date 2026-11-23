using System.Text;
using CodeEditor.Core.Output;

namespace CodeEditor.Modules.Output.Services;

/// <summary>
/// Size-limited output channel: on overflow the oldest quarter of the text is dropped at a line boundary,
/// so memory stays bounded. Thread-safe.
/// </summary>
internal sealed class OutputChannel(string name) : IOutputChannel
{
    /// <summary>In chars (~2 MB): enough for a session log or a long build.</summary>
    public const int MaxLength = 1_000_000;

    private const int TrimDivisor = 4;

    private readonly Lock _lock = new();
    private readonly StringBuilder _text = new();
    private long _version;

    // Grows when text is removed: a reader that shows the removed part has to start over.
    private long _epoch;

    public string Name { get; } = name;

    public long Version => Interlocked.Read(ref _version);

    public void AppendLine(string text)
    {
        lock (_lock)
        {
            _text.Append(text).Append('\n');
            TrimIfNeeded();
        }

        Interlocked.Increment(ref _version);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _text.Clear();
            _epoch++;
        }

        Interlocked.Increment(ref _version);
    }

    public string Snapshot()
    {
        lock (_lock)
        {
            return _text.ToString();
        }
    }

    public OutputDelta ReadSince(OutputCursor cursor)
    {
        lock (_lock)
        {
            var end = new OutputCursor(_epoch, _text.Length);
            return cursor.Epoch == _epoch && cursor.Position <= _text.Length
                ? new OutputDelta(_text.ToString((int)cursor.Position, _text.Length - (int)cursor.Position), IsReset: false, end)
                : new OutputDelta(_text.ToString(), IsReset: true, end);
        }
    }

    private void TrimIfNeeded()
    {
        if (_text.Length <= MaxLength)
        {
            return;
        }

        var cut = _text.Length / TrimDivisor;
        while (cut < _text.Length && _text[cut - 1] != '\n')
        {
            cut++;
        }

        _text.Remove(0, cut);
        _epoch++;
    }
}
