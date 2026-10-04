using System.Security.Cryptography;
using System.Text;
using CodeEditor.Core.Files;

namespace CodeEditor.Core.Storage;

/// <summary>
/// Secrets encrypted with DPAPI for the current Windows user: only that user on this machine can decrypt them.
/// Each secret is a <c>secrets/&lt;name&gt;.bin</c> file in the user data folder.
/// </summary>
public sealed class DpapiSecretStore(IFileSystem fileSystem, UserDataPaths paths) : ISecretStore
{
    private const string Folder = "secrets";

    // Extra entropy: another program of the same user cannot decrypt the file with a blind DPAPI call.
    private static readonly byte[] Entropy = "CodeEditor.Secrets.v1"u8.ToArray();

    public string? Get(string name)
    {
        var path = PathOf(name);
        if (!OperatingSystem.IsWindows() || !fileSystem.FileExists(path))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(fileSystem.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            // A file from another machine or user counts as a missing key.
            return null;
        }
    }

    public void Set(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The protected key store is available only on Windows.");
        }

        var folder = paths.File(Folder);
        if (!fileSystem.DirectoryExists(folder))
        {
            fileSystem.CreateDirectory(folder);
        }

        fileSystem.WriteAllBytesAtomic(PathOf(name), ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
    }

    public void Remove(string name)
    {
        var path = PathOf(name);
        if (fileSystem.FileExists(path))
        {
            fileSystem.DeleteFile(path);
        }
    }

    private string PathOf(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"Invalid secret name '{name}'.", nameof(name));
        }

        return Path.Combine(paths.File(Folder), name + ".bin");
    }
}
