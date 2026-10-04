using System.Globalization;
using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Files;

/// <summary>
/// File system on disk.
/// </summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    private const int ReplaceAttempts = 3;

    private static readonly TimeSpan ReplaceRetryDelay = TimeSpan.FromMilliseconds(50);

    private static readonly EnumerationOptions ImmediateChildren = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.System,
    };

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<FileSystemEntry> EnumerateEntries(string directory) =>
        new DirectoryInfo(directory)
            .EnumerateFileSystemInfos("*", ImmediateChildren)
            .Select(info => new FileSystemEntry(info.Name, info.FullName, info is DirectoryInfo));

    public string ReadAllText(string path)
    {
        using var reader = new StreamReader(OpenShared(path));
        return reader.ReadToEnd();
    }

    public byte[] ReadAllBytes(string path)
    {
        using var stream = OpenShared(path);
        var bytes = new byte[stream.Length];
        var read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        return read == bytes.Length ? bytes : bytes[..read];
    }

    public void WriteAllBytesAtomic(string path, byte[] bytes)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path))
            {
                // Replace keeps the original file's attributes and permissions.
                ReplaceWithRetry(temporary, path);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    // An antivirus, indexer or another editor's watcher may briefly hold the file, so retry a couple of times.
    private static void ReplaceWithRetry(string temporary, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Replace(temporary, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                return;
            }
            catch (IOException) when (attempt < ReplaceAttempts)
            {
                Thread.Sleep(ReplaceRetryDelay * attempt);
            }
        }
    }

    public void DeleteFile(string path) => File.Delete(path);

    public long GetFileLength(string path) => new FileInfo(path).Length;

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    public void CreateFile(string path)
    {
        using var _ = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
    }

    public void CreateDirectory(string path)
    {
        if (Directory.Exists(path) || File.Exists(path))
        {
            throw AlreadyExists(path);
        }

        Directory.CreateDirectory(path);
    }

    public void Move(string source, string destination)
    {
        // A case-only rename ("readme" to "README") is not a conflict even though the path "exists".
        var caseOnlyRename = string.Equals(source, destination, StringComparison.OrdinalIgnoreCase);
        if (!caseOnlyRename && (File.Exists(destination) || Directory.Exists(destination)))
        {
            throw AlreadyExists(destination);
        }

        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
        }
        else
        {
            File.Move(source, destination);
        }
    }

    public void CopyFile(string source, string destination)
    {
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw AlreadyExists(destination);
        }

        File.Copy(source, destination, overwrite: false);
    }

    public void DeleteToRecycleBin(string path) => RecycleBin.Delete(path);

    /// <summary>
    /// Non-blocking read: a file another process is writing (a log, a build log) still opens, as in VS Code.
    /// <c>File.ReadAllBytes</c> fails with a sharing violation in that case.
    /// </summary>
    private static FileStream OpenShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    public IFileWatcher Watch(string directory, Func<string, bool> isExcluded) =>
        new PhysicalFileWatcher(directory, isExcluded);

    private static IOException AlreadyExists(string path) =>
        new(string.Format(CultureInfo.CurrentCulture, Strings.PathAlreadyExists, path));
}
