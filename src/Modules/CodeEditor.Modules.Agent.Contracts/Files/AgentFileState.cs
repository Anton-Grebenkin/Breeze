using System.Collections.Concurrent;
using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Resources;

namespace CodeEditor.Modules.Agent.Contracts.Files;

/// <summary>
/// File state of the current chat (<see cref="IAgentFileState"/>): the last text version of each file known to the
/// agent, and the windows it read. A version is a text hash: cheaper to compare than storing copies. Reads run in
/// parallel, so the dictionaries are thread-safe. A new or reopened chat calls <see cref="Reset"/>, which forgets what
/// the agent read but keeps <see cref="Changes"/>: edits awaiting review stay highlighted until the user accepts or
/// rejects them, as in Cursor (ADR 0040).
/// </summary>
public sealed class AgentFileState : IAgentFileState
{
    private readonly ConcurrentDictionary<string, int> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<(string Path, int First, int Last, int Version), int> _windows = new();
    private readonly ConcurrentDictionary<string, string?> _originals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _forgotten = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _rejected = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncLocal<ConcurrentDictionary<(string Path, int First, int Last, int Version), int>?> _isolated = new();

    public IReadOnlyDictionary<string, string?> Changes => _originals;

    /// <summary>Files whose text version the agent knows: it read or edited them.</summary>
    public IReadOnlyCollection<string> KnownFiles => [.. _known.Keys];

    public event EventHandler<string>? Written;

    public event EventHandler<string?>? ChangesChanged;

    public bool RecordRead(string path, string text, int firstLine, int lastLine)
    {
        ArgumentNullException.ThrowIfNull(text);
        var version = Version(text);
        var window = (path.ToUpperInvariant(), firstLine, lastLine, version);
        if (_isolated.Value is { } isolated)
        {
            return IsFirstRepeat(isolated, window);
        }

        _known[path] = version;
        return IsFirstRepeat(_windows, window);
    }

    // "Unchanged" only on the first repeat of a window: asking again means the text is needed. Otherwise models tend
    // to split reads into tiny windows (1–10, 11–20, …) to get around the reply.
    private static bool IsFirstRepeat(ConcurrentDictionary<(string Path, int First, int Last, int Version), int> windows, (string, int, int, int) window) =>
        windows.AddOrUpdate(window, 1, (_, reads) => reads + 1) == 2;

    public string? CheckEditable(string path, string relativePath, string currentText)
    {
        ArgumentNullException.ThrowIfNull(currentText);
        if (!_known.TryGetValue(path, out var version))
        {
            return Format(_forgotten.ContainsKey(path) ? Strings.FileForgottenAfterCompaction : Strings.FileNotRead, relativePath);
        }

        return version == Version(currentText) ? null : Format(Strings.FileChangedSinceRead, relativePath);
    }

    private static string Format(string format, string relativePath) =>
        string.Format(CultureInfo.CurrentCulture, format, relativePath);

    public void RecordWrite(string path, string? text, string? previousText)
    {
        _originals.TryAdd(path, previousText);
        Written?.Invoke(this, path);
        if (text is null)
        {
            _known.TryRemove(path, out _);
            return;
        }

        _known[path] = Version(text);
    }

    /// <summary>The file was reverted or changed outside the agent: it must be re-read before the next edit.</summary>
    public void Forget(string path) => _known.TryRemove(path, out _);

    /// <summary>The text is exactly the file version the agent saw.</summary>
    public bool IsKnownVersion(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _known.TryGetValue(path, out var version) && version == Version(text);
    }

    public void RecordRejected(string path) => _rejected[path] = 0;

    /// <summary>Files where the user rejected agent edits since the previous call.</summary>
    public IReadOnlyCollection<string> TakeRejected()
    {
        var rejected = _rejected.Keys.ToList();
        foreach (var path in rejected)
        {
            _rejected.TryRemove(path, out _);
        }

        return rejected;
    }

    /// <summary>The user accepted the edits: the chat diff restarts from the current text.</summary>
    public void AcceptChanges()
    {
        _originals.Clear();
        ChangesChanged?.Invoke(this, null);
    }

    public void UpdateOriginal(string path, string original)
    {
        ArgumentNullException.ThrowIfNull(original);
        _originals[path] = original;
        ChangesChanged?.Invoke(this, path);
    }

    public void AcceptFile(string path)
    {
        _originals.TryRemove(path, out _);
        ChangesChanged?.Invoke(this, path);
    }

    /// <summary>
    /// Old tool results were collapsed, so the model may no longer have the read windows: a repeated read returns the
    /// text rather than "unchanged". Known versions stay, so edits are still checked against the text.
    /// </summary>
    public void ForgetWindows() => _windows.Clear();

    /// <summary>Context was compacted into a summary: the model no longer has the read text and must re-read before editing.</summary>
    public void ForgetReads()
    {
        foreach (var path in _known.Keys)
        {
            _forgotten[path] = 0;
        }

        _known.Clear();
        _windows.Clear();
    }

    /// <summary>
    /// Reads in this async flow do not count for the agent (the scout reads on its behalf): the scope has its own set of
    /// read windows and the agent's known versions stay unchanged. The main agent reading the same windows in parallel
    /// gets the text, not "unchanged". Call from an async method: the scope is visible only to its flow.
    /// </summary>
    public IDisposable IsolateReads()
    {
        var previous = _isolated.Value;
        _isolated.Value = new();
        return new ReadScope(() => _isolated.Value = previous);
    }

    public void Reset()
    {
        _known.Clear();
        _windows.Clear();
        _forgotten.Clear();
        _rejected.Clear();
    }

    /// <summary>Edits awaiting review of another folder or restored after a restart replace the current ones.</summary>
    public void ReplaceChanges(IReadOnlyDictionary<string, string?> originals)
    {
        ArgumentNullException.ThrowIfNull(originals);
        _originals.Clear();
        foreach (var (path, original) in originals)
        {
            _originals[path] = original;
        }

        ChangesChanged?.Invoke(this, null);
    }

    private static int Version(string text) => HashCode.Combine(text.Length, text.GetHashCode(StringComparison.Ordinal));

    private sealed class ReadScope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
