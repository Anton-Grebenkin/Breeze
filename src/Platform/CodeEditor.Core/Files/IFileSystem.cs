namespace CodeEditor.Core.Files;

/// <summary>
/// File system: the disk in the app, memory in tests, a remote one later.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>Immediate children of a folder (not recursive). Inaccessible entries are skipped.</summary>
    IEnumerable<FileSystemEntry> EnumerateEntries(string directory);

    string ReadAllText(string path);

    byte[] ReadAllBytes(string path);

    /// <summary>Writes to a temporary file alongside and renames it, so a failure leaves the old file intact.</summary>
    void WriteAllBytesAtomic(string path, byte[] bytes);

    long GetFileLength(string path);

    DateTime GetLastWriteTimeUtc(string path);

    /// <summary>Creates an empty file.</summary>
    /// <exception cref="IOException">The file already exists.</exception>
    void CreateFile(string path);

    void CreateDirectory(string path);

    /// <summary>Renames or moves a file or folder.</summary>
    /// <exception cref="IOException">The destination already exists.</exception>
    void Move(string source, string destination);

    /// <summary>Copies a file; folders are copied file by file by the caller.</summary>
    /// <exception cref="IOException">The destination already exists.</exception>
    void CopyFile(string source, string destination);

    /// <summary>Moves a file or folder to the Recycle Bin, so the deletion can be undone.</summary>
    void DeleteToRecycleBin(string path);

    /// <summary>
    /// Permanently deletes an app-owned file (a secret, a temporary file); user files go only to the Recycle Bin.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>Watches a folder recursively. Paths matching <paramref name="isExcluded"/> are not reported.</summary>
    IFileWatcher Watch(string directory, Func<string, bool> isExcluded);
}
