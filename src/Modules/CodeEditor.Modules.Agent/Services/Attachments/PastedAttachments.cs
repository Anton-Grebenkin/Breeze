using System.Globalization;
using System.Text;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;

namespace CodeEditor.Modules.Agent.Services.Attachments;

/// <summary>
/// Clipboard content attached to a message: a screenshot or a long text becomes a file, so it takes the same path as a
/// picked file. Files go to <c>.breeze/agent/attachments/</c> (ignored by git) or, without an open folder, to the user
/// data folder.
/// </summary>
public sealed class PastedAttachments(IWorkspace workspace, IFileSystem fileSystem, UserDataPaths paths, TimeProvider time)
{
    public const string FolderName = "attachments";

    /// <summary>
    /// A paste this long goes as a file rather than into the input box, which shows about ten lines; a log or a dump is
    /// easier to remove as a chip than to scroll through.
    /// </summary>
    public const int LongTextCharacters = 10_000;

    public const int LongTextLines = 100;

    private const string ImagePrefix = "screenshot";
    private const string TextPrefix = "pasted";

    public static bool IsLongText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length >= LongTextCharacters || text.AsSpan().Count('\n') + 1 >= LongTextLines;
    }

    /// <returns>The saved file's path.</returns>
    public string SaveImage(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        return Save(ImagePrefix, ".png", png);
    }

    /// <returns>The saved file's path.</returns>
    public string SaveText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Save(TextPrefix, ".txt", Encoding.UTF8.GetBytes(text));
    }

    private string Save(string prefix, string extension, byte[] data)
    {
        var folder = Folder();
        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var path = Path.Combine(folder, $"{prefix}-{stamp}{extension}");
        for (var copy = 2; fileSystem.FileExists(path); copy++)
        {
            path = Path.Combine(folder, $"{prefix}-{stamp}-{copy}{extension}");
        }

        fileSystem.WriteAllBytesAtomic(path, data);
        return path;
    }

    private string Folder()
    {
        if (AgentDataFolder.PathOf(workspace, FolderName) is { } inWorkspace)
        {
            AgentDataFolder.Ensure(workspace, fileSystem, inWorkspace);
            return inWorkspace;
        }

        var global = paths.File("agent-" + FolderName);
        if (!fileSystem.DirectoryExists(global))
        {
            fileSystem.CreateDirectory(global);
        }

        return global;
    }
}
