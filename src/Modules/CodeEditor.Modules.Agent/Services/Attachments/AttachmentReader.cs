using System.Collections.Frozen;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Files;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Attachments;

/// <summary>
/// Files attached to a message for the agent: text files as content in the message (an <c>&lt;attachment&gt;</c> block),
/// images as pictures. A workspace file then counts as read, so the model can edit it without reading it again. Files
/// are read in the background.
/// </summary>
public sealed class AttachmentReader(IFileSystem fileSystem, IWorkspace workspace, AgentFileState fileState)
{
    /// <summary>Text characters sent in the message (~25K tokens); the model reads the rest with a tool.</summary>
    public const int MaxTextCharacters = 100_000;

    /// <summary>Image size limit: Anthropic rejects larger images.</summary>
    public const long MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>Larger files are not opened: such text would not fit in a message anyway.</summary>
    public const long MaxFileBytes = 20 * 1024 * 1024;

    // Text differs from a binary file by having no zero bytes at the start.
    private const int ProbeBytes = 8 * 1024;

    private static readonly FrozenDictionary<string, string> ImageTypes = new Dictionary<string, string>
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // Binary formats that may have no zero bytes at the start.
    private static readonly FrozenSet<string> BinaryExtensions = FrozenSet.ToFrozenSet(
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".7z", ".rar", ".gz", ".exe", ".dll", ".pdb", ".bmp", ".ico"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Media type of an image models understand, by extension; <c>null</c> for other files.</summary>
    public static string? ImageType(string path) => ImageTypes.GetValueOrDefault(Path.GetExtension(path));

    public Task<AttachmentKind> ClassifyAsync(string path, CancellationToken cancellationToken) =>
        Task.Run(() => Classify(path), cancellationToken);

    /// <summary>Message content: a block per text file, a label and a picture per image.</summary>
    public Task<IReadOnlyList<AIContent>> ReadAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.Count == 0
            ? Task.FromResult<IReadOnlyList<AIContent>>([])
            : Task.Run<IReadOnlyList<AIContent>>(() => [.. paths.SelectMany(ReadOrNote)], cancellationToken);
    }

    /// <summary>Path for the model and the feed: workspace-relative, or full outside the workspace.</summary>
    public string Display(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return IsInWorkspace(path) ? workspace.RelativePath(path) : path;
    }

    // An inaccessible or locked file is not attached either; the user sees it in the status bar.
    private AttachmentKind Classify(string path)
    {
        try
        {
            return ClassifyUnchecked(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AttachmentKind.Unsupported;
        }
    }

    private AttachmentKind ClassifyUnchecked(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            return AttachmentKind.Unsupported;
        }

        var extension = Path.GetExtension(path);
        var length = fileSystem.GetFileLength(path);
        if (ImageTypes.ContainsKey(extension))
        {
            return length <= MaxImageBytes ? AttachmentKind.Image : AttachmentKind.Unsupported;
        }

        return !BinaryExtensions.Contains(extension) && length <= MaxFileBytes && IsText(fileSystem.ReadAllBytes(path))
            ? AttachmentKind.Text
            : AttachmentKind.Unsupported;
    }

    private static bool IsText(byte[] bytes) => bytes.AsSpan(0, Math.Min(bytes.Length, ProbeBytes)).IndexOf((byte)0) < 0;

    // The file may have been deleted or locked after attaching: the model learns it is missing and the turn goes on.
    private List<AIContent> ReadOrNote(string path)
    {
        try
        {
            return [.. Read(path)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [NotAttached(Display(path))];
        }
    }

    private static TextContent NotAttached(string name) =>
        new($"<attachment path=\"{name}\">(not attached: the file is missing, too large or not a text file)</attachment>");

    private IEnumerable<AIContent> Read(string path)
    {
        var name = Display(path).Replace('"', '\'');
        switch (Classify(path))
        {
            case AttachmentKind.Image:
                yield return new TextContent($"<attachment path=\"{name}\" type=\"image\" />");
                yield return new DataContent(fileSystem.ReadAllBytes(path), ImageTypes[Path.GetExtension(path)]) { Name = name };
                break;
            case AttachmentKind.Text:
                yield return new TextContent(TextBlock(path, name));
                break;
            default:
                yield return NotAttached(name);
                break;
        }
    }

    private string TextBlock(string path, string name)
    {
        var text = fileSystem.ReadAllText(path);
        var shown = text.Length > MaxTextCharacters ? text[..MaxTextCharacters] : text;
        var lines = LineCount(shown);
        if (IsInWorkspace(path))
        {
            fileState.RecordRead(path, text, 1, lines);
        }

        var part = shown.Length < text.Length
            ? string.Create(CultureInfo.InvariantCulture, $" shown=\"lines 1-{lines} of {LineCount(text)}\"")
            : string.Empty;
        return $"<attachment path=\"{name}\"{part}>\n{shown}\n</attachment>";
    }

    private static int LineCount(string text) => text.Length == 0 ? 0 : text.AsSpan().Count('\n') + (text.EndsWith('\n') ? 0 : 1);

    private bool IsInWorkspace(string path)
    {
        if (workspace.Root is null)
        {
            return false;
        }

        var relative = workspace.RelativePath(path);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}
