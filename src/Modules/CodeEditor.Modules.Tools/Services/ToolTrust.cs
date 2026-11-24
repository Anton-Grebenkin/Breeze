using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Tools.Services;

/// <summary>
/// Tools the user allowed, by the hash of everything in the tool folder: the same code runs without asking, new or
/// changed code asks again. The list lives in the user data folder, so a cloned repository cannot trust itself.
/// Personal tools are the user's own and always trusted.
/// </summary>
public sealed partial class ToolTrust(IFileSystem fileSystem, UserDataPaths paths, ILogger<ToolTrust> logger)
{
    public const string FileName = "trusted-tools.json";

    private const string Prefix = "sha256:";

    private readonly Lock _lock = new();
    private HashSet<string>? _trusted;

    public bool IsTrusted(ToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool.IsPersonal || IsTrusted(Hash(tool));
    }

    public bool IsTrusted(string hash)
    {
        lock (_lock)
        {
            return Trusted().Contains(hash);
        }
    }

    public void Trust(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        lock (_lock)
        {
            if (!Trusted().Add(hash))
            {
                return;
            }

            try
            {
                var json = JsonSerializer.SerializeToUtf8Bytes(_trusted!.Order(StringComparer.Ordinal).ToArray());
                fileSystem.WriteAllBytesAtomic(paths.File(FileName), json);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LogNotSaved(logger, exception.Message);
            }
        }
    }

    /// <summary>
    /// Hash of every file in the tool folder with its relative path: the contents of the tool's own files, and the size
    /// and write time of installed packages and build output. Those run too, but may be hundreds of megabytes, so a
    /// reinstall or a replaced file asks again without reading them. O(size of the tool's own files + file count).
    /// </summary>
    public string Hash(ToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (file, installed) in Files(tool.Folder, installed: false).OrderBy(static entry => entry.Path, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(tool.Folder, file).Replace('\\', '/') + "\0"));
            hash.AppendData(installed
                ? Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{fileSystem.GetFileLength(file)}:{fileSystem.GetLastWriteTimeUtc(file).Ticks}\0"))
                : fileSystem.ReadAllBytes(file));
        }

        return Prefix + Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private IEnumerable<(string Path, bool Installed)> Files(string folder, bool installed) =>
        fileSystem.EnumerateEntries(folder).SelectMany(entry => entry.IsDirectory
            ? Files(entry.FullPath, installed || PathExclusions.DefaultExcludedNames.Contains(entry.Name))
            : [(entry.FullPath, installed)]);

    private HashSet<string> Trusted()
    {
        if (_trusted is not null)
        {
            return _trusted;
        }

        _trusted = new HashSet<string>(StringComparer.Ordinal);
        var path = paths.File(FileName);
        try
        {
            if (fileSystem.FileExists(path) && JsonSerializer.Deserialize<string[]>(fileSystem.ReadAllBytes(path)) is { } hashes)
            {
                _trusted.UnionWith(hashes);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            LogNotRead(logger, exception.Message);
        }

        return _trusted;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Trusted tools were not saved: {Error}")]
    private static partial void LogNotSaved(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Trusted tools were not read: {Error}")]
    private static partial void LogNotRead(ILogger logger, string error);
}
