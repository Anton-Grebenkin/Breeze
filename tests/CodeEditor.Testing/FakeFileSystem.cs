using CodeEditor.Core.Files;

namespace CodeEditor.Testing;

/// <summary>
/// In-memory file system. Paths compare case-insensitively, as on Windows.
/// Tests drive watching through <see cref="Watchers"/>; paths sent to the Recycle Bin are recorded.
/// </summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string?> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _bytes = new(StringComparer.OrdinalIgnoreCase);

    public List<FakeFileWatcher> Watchers { get; } = [];

    public List<string> RecycledPaths { get; } = [];

    /// <summary>Adds the folder with all its parents.</summary>
    public FakeFileSystem AddDirectory(string path)
    {
        for (var current = Normalize(path); current is not null; current = Path.GetDirectoryName(current))
        {
            _entries.TryAdd(current, null);
        }

        return this;
    }

    public FakeFileSystem AddFile(string path, string content = "")
    {
        var full = Normalize(path);
        AddDirectory(Path.GetDirectoryName(full)!);
        _entries[full] = content;
        _bytes.Remove(full);
        return this;
    }

    public bool FileExists(string path) => _entries.TryGetValue(Normalize(path), out var content) && content is not null;

    public bool DirectoryExists(string path) => _entries.TryGetValue(Normalize(path), out var content) && content is null;

    public IEnumerable<FileSystemEntry> EnumerateEntries(string directory)
    {
        var parent = Normalize(directory);
        return _entries
            .Where(entry => string.Equals(Path.GetDirectoryName(entry.Key), parent, StringComparison.OrdinalIgnoreCase))
            .Select(entry => new FileSystemEntry(Path.GetFileName(entry.Key), entry.Key, entry.Value is null))
            .ToArray();
    }

    public string ReadAllText(string path) =>
        _entries.TryGetValue(Normalize(path), out var content) && content is not null
            ? content
            : throw new FileNotFoundException("Нет файла.", path);

    public Dictionary<string, DateTime> WriteTimes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public byte[] ReadAllBytes(string path) => _bytes.TryGetValue(Normalize(path), out var bytes)
        ? bytes
        : System.Text.Encoding.UTF8.GetBytes(ReadAllText(path));

    public void WriteAllBytesAtomic(string path, byte[] bytes)
    {
        var full = Normalize(path);
        AddFile(full, System.Text.Encoding.UTF8.GetString(bytes));
        _bytes[full] = bytes;
        Touch(full);
    }

    /// <summary>Adds a file with exact bytes, for encoding tests.</summary>
    public FakeFileSystem AddBytes(string path, byte[] bytes)
    {
        var full = Normalize(path);
        AddFile(full, string.Empty);
        _bytes[full] = bytes;
        return this;
    }

    public long GetFileLength(string path) => ReadAllBytes(path).LongLength;

    public DateTime GetLastWriteTimeUtc(string path) => WriteTimes.GetValueOrDefault(Normalize(path), DateTime.UnixEpoch);

    /// <summary>Advances the file's write time, like an external write.</summary>
    public void Touch(string path) =>
        WriteTimes[Normalize(path)] = GetLastWriteTimeUtc(path).AddSeconds(1);

    public void CreateFile(string path)
    {
        EnsureFree(path);
        AddFile(path);
    }

    public void CreateDirectory(string path)
    {
        EnsureFree(path);
        AddDirectory(path);
    }

    public void Move(string source, string destination)
    {
        var from = Normalize(source);
        if (!string.Equals(from, Normalize(destination), StringComparison.OrdinalIgnoreCase))
        {
            EnsureFree(destination);
        }

        foreach (var key in _entries.Keys.Where(key => IsSameOrInside(key, from)).ToArray())
        {
            var content = _entries[key];
            var moved = Normalize(destination) + key[from.Length..];
            _entries.Remove(key);
            _entries[moved] = content;
            if (_bytes.Remove(key, out var bytes))
            {
                _bytes[moved] = bytes;
            }
        }
    }

    public void CopyFile(string source, string destination)
    {
        var from = Normalize(source);
        if (!FileExists(from))
        {
            throw new FileNotFoundException("No such file.", source);
        }

        EnsureFree(destination);
        AddFile(destination, _entries[from]!);
        if (_bytes.TryGetValue(from, out var bytes))
        {
            _bytes[Normalize(destination)] = bytes;
        }
    }

    public void DeleteFile(string path)
    {
        _entries.Remove(Normalize(path));
        _bytes.Remove(Normalize(path));
    }

    public void DeleteToRecycleBin(string path)
    {
        var full = Normalize(path);
        foreach (var key in _entries.Keys.Where(key => IsSameOrInside(key, full)).ToArray())
        {
            _entries.Remove(key);
        }

        RecycledPaths.Add(full);
    }

    public IFileWatcher Watch(string directory, Func<string, bool> isExcluded)
    {
        var watcher = new FakeFileWatcher(isExcluded);
        Watchers.Add(watcher);
        return watcher;
    }

    private void EnsureFree(string path)
    {
        if (_entries.ContainsKey(Normalize(path)))
        {
            throw new IOException($"«{path}» уже существует.");
        }
    }

    private static bool IsSameOrInside(string key, string root) =>
        string.Equals(key, root, StringComparison.OrdinalIgnoreCase)
        || key.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
