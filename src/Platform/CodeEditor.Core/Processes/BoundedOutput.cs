using System.Globalization;
using System.Text;
using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Processes;

/// <summary>
/// Memory-bounded process output: the first <see cref="HeadCharacters"/> characters and the last
/// <see cref="TailCharacters"/>; the middle is dropped with a line count. For builds and tests the start (what ran)
/// and the end (errors, summary) matter. Lines arrive from two threads (stdout, stderr), hence the lock.
/// </summary>
public sealed class BoundedOutput
{
    public const int HeadCharacters = 16_000;
    public const int TailCharacters = 48_000;

    private readonly Lock _gate = new();
    private readonly StringBuilder _head = new();
    private readonly Queue<string> _tail = new();
    private int _tailLength;
    private int _dropped;

    public void Add(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        lock (_gate)
        {
            if (_tail.Count == 0 && _head.Length + line.Length < HeadCharacters)
            {
                _head.Append(line).Append('\n');
                return;
            }

            _tail.Enqueue(line);
            _tailLength += line.Length + 1;
            while (_tailLength > TailCharacters && _tail.Count > 1)
            {
                _tailLength -= _tail.Dequeue().Length + 1;
                _dropped++;
            }
        }
    }

    public string Text
    {
        get
        {
            lock (_gate)
            {
                var text = new StringBuilder().Append(_head);
                if (_dropped > 0)
                {
                    text.Append(string.Format(CultureInfo.CurrentCulture, Strings.LinesSkipped, _dropped)).Append('\n');
                }

                foreach (var line in _tail)
                {
                    text.Append(line).Append('\n');
                }

                return text.ToString();
            }
        }
    }
}
