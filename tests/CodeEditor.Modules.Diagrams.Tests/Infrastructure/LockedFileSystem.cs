using CodeEditor.Core.Files;

namespace CodeEditor.Modules.Diagrams.Tests.Infrastructure;

/// <summary>File system where every file is locked by another process: reads throw <see cref="IOException"/>.</summary>
internal sealed class LockedFileSystem : IFileSystem
{
    public const string Message = "Файл занят другим процессом.";

    public bool FileExists(string path) => true;

    public bool DirectoryExists(string path) => true;

    public IEnumerable<FileSystemEntry> EnumerateEntries(string directory) => [];

    public string ReadAllText(string path) => throw new IOException(Message);

    public byte[] ReadAllBytes(string path) => throw new IOException(Message);

    public void WriteAllBytesAtomic(string path, byte[] bytes) => throw new IOException(Message);

    public long GetFileLength(string path) => 0;

    public DateTime GetLastWriteTimeUtc(string path) => DateTime.UnixEpoch;

    public void CreateFile(string path) => throw new IOException(Message);

    public void CreateDirectory(string path) => throw new IOException(Message);

    public void Move(string source, string destination) => throw new IOException(Message);

    public void CopyFile(string source, string destination) => throw new IOException(Message);

    public void DeleteToRecycleBin(string path) => throw new IOException(Message);

    public void DeleteFile(string path) => throw new IOException(Message);

    public IFileWatcher Watch(string directory, Func<string, bool> isExcluded) => throw new NotSupportedException();
}
