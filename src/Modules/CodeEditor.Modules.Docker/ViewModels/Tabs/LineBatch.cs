using CodeEditor.Core.Threading;

namespace CodeEditor.Modules.Docker.ViewModels.Tabs;

/// <summary>
/// Moves process lines from background threads to the UI thread in batches: until the UI thread takes a batch, new
/// lines accumulate in it, so a thousand lines at once cause one tab update, not a thousand. A closed batch (the log
/// stream was replaced or the tab closed) delivers nothing more. If the UI thread falls behind, only the last lines stay.
/// </summary>
internal sealed class LineBatch(IUiDispatcher dispatcher, int maxPending, Action<IReadOnlyList<string>> deliver)
{
    private readonly Lock _gate = new();
    private List<string> _pending = [];
    private bool _scheduled;
    private bool _closed;

    /// <summary>Callable from any thread.</summary>
    public void Add(string line)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _pending.Add(line);
            if (_pending.Count > 2 * maxPending)
            {
                _pending.RemoveRange(0, _pending.Count - maxPending);
            }

            if (_scheduled)
            {
                return;
            }

            _scheduled = true;
        }

        dispatcher.Post(Flush);
    }

    /// <summary>Delivers the pending lines now; on the UI thread.</summary>
    public void Flush()
    {
        List<string> lines;
        lock (_gate)
        {
            _scheduled = false;
            if (_closed || _pending.Count == 0)
            {
                return;
            }

            lines = _pending;
            _pending = [];
        }

        deliver(lines);
    }

    public void Close()
    {
        lock (_gate)
        {
            _closed = true;
            _pending = [];
        }
    }
}
