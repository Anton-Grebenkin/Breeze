using System.Globalization;
using System.Security.Cryptography;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;

namespace CodeEditor.Modules.Documents.Services;

/// <summary>
/// Document files for agent tools: paths only inside the workspace and never to secrets or service folders, format by
/// extension, whole-file reads and writes with errors the model understands. Writes go through a temp file, so a failure
/// leaves the old document intact. A file open in Word or Excel isn't overwritten; the model learns it's locked.
/// </summary>
public sealed class DocumentFiles(IWorkspace workspace, IFileSystem fileSystem)
{
    /// <exception cref="AgentToolException">
    /// The path is outside the workspace or points to secrets or a service folder, or the format is unsupported.
    /// </exception>
    public DocumentPath Resolve(string path, bool forWriting)
    {
        var full = WorkspacePaths.Resolve(workspace, path);
        var relative = workspace.RelativePath(full);
        if (forWriting)
        {
            SensitivePaths.EnsureWritable(relative);
        }
        else
        {
            SensitivePaths.EnsureReadable(relative);
        }

        var kind = DocumentKinds.Of(full);
        return kind == DocumentKind.None
            ? throw new AgentToolException(Format(Strings.UnsupportedFormat, path, DocumentKinds.Supported))
            : new DocumentPath(full, relative, kind);
    }

    public bool Exists(DocumentPath path) => fileSystem.FileExists(path.Full);

    /// <exception cref="AgentToolException">The file doesn't exist or can't be read.</exception>
    public byte[] Read(DocumentPath path)
    {
        if (!fileSystem.FileExists(path.Full))
        {
            throw new AgentToolException(Format(fileSystem.DirectoryExists(path.Full) ? Strings.PathIsFolder : Strings.FileNotFound, path.Relative));
        }

        try
        {
            return fileSystem.ReadAllBytes(path.Full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AgentToolException(Format(Strings.FileNotReadable, path.Relative, exception.Message));
        }
    }

    /// <param name="recycleExisting">
    /// Move the old file to the Recycle Bin instead of overwriting it, so a replaced document can be restored.
    /// </param>
    /// <exception cref="AgentToolException">The file is locked by another program or access is denied.</exception>
    public void Write(DocumentPath path, byte[] bytes, bool recycleExisting = false)
    {
        try
        {
            var folder = Path.GetDirectoryName(path.Full)!;
            if (!fileSystem.DirectoryExists(folder))
            {
                fileSystem.CreateDirectory(folder);
            }

            if (recycleExisting && fileSystem.FileExists(path.Full))
            {
                fileSystem.DeleteToRecycleBin(path.Full);
            }

            fileSystem.WriteAllBytesAtomic(path.Full, bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AgentToolException(Format(Strings.FileNotWritable, path.Relative, exception.Message));
        }
    }

    /// <summary>Content fingerprint: tells whether the document changed since the user saw the card.</summary>
    public string Fingerprint(DocumentPath path)
    {
        if (!fileSystem.FileExists(path.Full))
        {
            return string.Empty;
        }

        try
        {
            return Convert.ToHexString(SHA256.HashData(fileSystem.ReadAllBytes(path.Full)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AgentToolException(Format(Strings.FileNotReadable, path.Relative, exception.Message));
        }
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
