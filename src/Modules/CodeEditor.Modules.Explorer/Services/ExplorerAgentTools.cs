using System.ComponentModel;
using System.Globalization;
using System.Text;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Text;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Explorer.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Explorer.Services;

/// <summary>
/// Agent tools for workspace files: list a folder, read a file, find files by glob.
/// Files modified in the editor are read from the buffer, so the agent sees what the user sees.
/// </summary>
public sealed class ExplorerAgentTools(
    IWorkspace workspace,
    IFileSystem fileSystem,
    IFileIndex index,
    IDocumentService documents,
    IUiDispatcher dispatcher,
    IAgentFileState fileState) : IAgentToolProvider
{
    public const string ReadFileName = "read_file";
    public const string ListDirName = "list_dir";
    public const string FindFilesName = "find_files";

    public const int MaxEntries = 500;
    /// <summary>
    /// The default page covers a typical file whole: smaller pages made the agent read large files in 2–3 calls.
    /// </summary>
    public const int DefaultMaxLines = 1000;
    public const int MaxLinesPerRead = 2000;

    /// <summary>Character cap per read (~10K tokens), so long lines don't bloat the context (ADR 0012).</summary>
    public const int MaxCharactersPerRead = 40_000;

    /// <summary>Line number and tab before the line text.</summary>
    private const int LinePrefixLength = 8;
    public const int MaxDepth = 3;
    public const int MaxFiles = 200;

    /// <summary>How many paths to list when a file name is ambiguous.</summary>
    private const int MaxAmbiguous = 8;

    public IEnumerable<AITool> CreateTools() =>
    [
        new ReadOnlyAIFunction(AIFunctionFactory.Create(ListDirectory, ListDirName, "Lists files and folders of a workspace folder (folders end with '/'). Hidden: .git, bin, obj, node_modules, .gitignore rules.")),
        new ReadOnlyAIFunction(AIFunctionFactory.Create(ReadFileAsync, ReadFileName, "Reads a text file of the workspace, up to 1000 lines (about 40k characters) per call — usually the whole file; use startLine only for the remainder. Each line is prefixed with its number and a tab; the prefix is not part of the file. Unsaved editor changes are included.")),
        new ReadOnlyAIFunction(AIFunctionFactory.Create(FindFiles, FindFilesName, "Finds workspace files by glob ('**/*.cs', 'src/**/Program.cs') or by a part of the relative path. Returns relative paths.")),
    ];

    private string ListDirectory(
        [Description("Folder relative to the workspace root; '.' for the root.")] string path = ".",
        [Description("How many levels to show, 1–3: an overview of the project in one call.")] int depth = 1)
    {
        var folder = WorkspacePaths.Resolve(workspace, path);
        if (!fileSystem.DirectoryExists(folder))
        {
            throw new AgentToolException(Format(Strings.ToolFolderNotFound, path) + Similar(path));
        }

        var entries = new List<string>();
        var truncated = AppendEntries(entries, folder, Math.Clamp(depth, 1, MaxDepth), indent: string.Empty);
        return entries.Count == 0
            ? Strings.ToolEmptyFolder
            : string.Join('\n', entries) + (truncated ? "\n" + Format(Strings.ToolTooManyEntries, MaxEntries) : string.Empty);
    }

    /// <returns><c>true</c> if <see cref="MaxEntries"/> was reached.</returns>
    private bool AppendEntries(List<string> entries, string folder, int depth, string indent)
    {
        var children = fileSystem.EnumerateEntries(folder)
            .Where(entry => !workspace.IsExcluded(entry.FullPath, entry.IsDirectory) && !SensitivePaths.IsAgentData(workspace.RelativePath(entry.FullPath)))
            .OrderBy(entry => entry.IsDirectory ? 0 : 1)
            .ThenBy(entry => entry.Name, NaturalStringComparer.Instance);
        foreach (var entry in children)
        {
            if (entries.Count == MaxEntries)
            {
                return true;
            }

            entries.Add(indent + (entry.IsDirectory ? entry.Name + "/" : entry.Name));
            if (entry.IsDirectory && depth > 1 && AppendEntries(entries, entry.FullPath, depth - 1, indent + "  "))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<string> ReadFileAsync(
        [Description("File path relative to the workspace root, or a file name / path ending that is unique in the workspace ('DictionaryRecord.cs').")] string path,
        [Description("First line to read, 1-based.")] int startLine = 1,
        [Description("Maximum number of lines to return.")] int maxLines = DefaultMaxLines)
    {
        var (file, resolvedNote) = ResolveFile(path);
        if (fileSystem.DirectoryExists(file))
        {
            // Models sometimes pass a folder: list it right away instead of failing and costing a turn.
            return Format(Strings.ToolFolderInsteadOfFile, path) + "\n" + ListDirectory(path, depth: 1);
        }

        var relative = workspace.RelativePath(file);
        SensitivePaths.EnsureReadable(relative);
        var text = await ReadTextAsync(file, path);
        if (text.Length == 0)
        {
            fileState.RecordRead(file, text, 1, 1);
            return Strings.ToolEmptyFile;
        }

        var lines = text.Split('\n');
        var first = Math.Clamp(startLine, 1, Math.Max(1, lines.Length));
        var count = Math.Clamp(maxLines, 1, MaxLinesPerRead);
        var last = LastLineWithin(lines, first, Math.Min(lines.Length, first + count - 1));
        if (fileState.RecordRead(file, text, first, last))
        {
            return Format(Strings.ToolLinesUnchanged, first, last, relative);
        }

        var output = new StringBuilder();
        for (var line = first; line <= last; line++)
        {
            var content = lines[line - 1].TrimEnd('\r');
            output.Append(CultureInfo.InvariantCulture, $"{line}\t{(content.Length > MaxCharactersPerRead ? content[..MaxCharactersPerRead] + "…" : content)}\n");
        }

        if (last < lines.Length)
        {
            output.Append(Format(Strings.ToolLinesPage, first, last, lines.Length, last + 1));
        }

        return resolvedNote + ToolOutput.Limit(output.ToString());
    }

    /// <summary>
    /// Resolves a model-supplied path as is or, if missing, to the only indexed file with that name or path ending
    /// ("DictionaryRecord.cs", "Domain/Dictionary.cs"); otherwise models ran text searches just to find the path.
    /// </summary>
    private (string File, string Note) ResolveFile(string path)
    {
        var file = WorkspacePaths.Resolve(workspace, path);
        if (fileSystem.FileExists(file) || fileSystem.DirectoryExists(file) || documents.TryGet(file, out _))
        {
            return (file, string.Empty);
        }

        var ending = path.Replace('\\', '/').TrimStart('.', '/');
        var suffix = "/" + ending;
        var candidates = index.Files
            .Where(candidate => !SensitivePaths.IsAgentData(candidate.RelativePath)
                && (candidate.RelativePath.Equals(ending, StringComparison.OrdinalIgnoreCase) || candidate.RelativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            .Take(MaxAmbiguous + 1)
            .ToList();
        return candidates.Count switch
        {
            1 => (candidates[0].FullPath, Format(Strings.ToolPathResolved, path, candidates[0].RelativePath) + "\n"),
            > 1 => throw new AgentToolException(Format(Strings.ToolPathAmbiguous, path, string.Join(", ", candidates.Take(MaxAmbiguous).Select(candidate => candidate.RelativePath)))),
            _ => (file, string.Empty),
        };
    }

    /// <summary>Last line of the page within the character cap; the first line always fits (reading truncates it).</summary>
    private static int LastLineWithin(string[] lines, int first, int last)
    {
        var used = 0;
        for (var line = first; line <= last; line++)
        {
            used += lines[line - 1].Length + LinePrefixLength;
            if (used > MaxCharactersPerRead && line > first)
            {
                return line - 1;
            }
        }

        return last;
    }

    private string FindFiles([Description("Glob like '**/*.cs' or a part of the path like 'Program'.")] string pattern)
    {
        if (workspace.Root is null)
        {
            throw new AgentToolException(Strings.ToolWorkspaceNotOpen);
        }

        var isGlob = pattern.AsSpan().IndexOfAny('*', '?') >= 0;
        var glob = isGlob ? GlobFilter.Parse(pattern) : GlobFilter.Empty;
        var matches = index.Files
            .Where(file => !SensitivePaths.IsAgentData(file.RelativePath))
            .Where(file => isGlob ? glob.Matches(file.RelativePath) : file.RelativePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .Select(file => file.RelativePath)
            .Order(NaturalStringComparer.Instance)
            .ToList();

        return matches.Count == 0
            ? Strings.ToolNoFilesFound + Similar(pattern)
            : string.Join('\n', matches.Take(MaxFiles)) + (matches.Count > MaxFiles ? "\n" + Format(Strings.ToolMoreFiles, matches.Count - MaxFiles) : string.Empty);
    }

    private async Task<string> ReadTextAsync(string file, string path)
    {
        string? unsaved = null;
        await dispatcher.InvokeAsync(() =>
        {
            if (documents.TryGet(file, out var document) && document.IsDirty)
            {
                unsaved = document.Buffer.GetText();
            }
        });

        if (unsaved is not null)
        {
            return unsaved;
        }

        if (!fileSystem.FileExists(file))
        {
            throw new AgentToolException(fileSystem.DirectoryExists(file)
                ? Format(Strings.ToolPathIsFolder, path)
                : Format(Strings.ToolFileNotFound, path) + Similar(path));
        }

        return TextFileCodec.Decode(fileSystem.ReadAllBytes(file))?.Text
            ?? throw new AgentToolException(Format(Strings.ToolBinaryFile, path));
    }

    // Similar paths for a glob or path with no match, so the model sees the real folder and file names.
    private string Similar(string pattern)
    {
        var similar = SimilarPaths.Suggest(index.Files.Select(file => file.RelativePath).Where(path => !SensitivePaths.IsAgentData(path)), pattern);
        return similar.Count == 0 ? string.Empty : Format(Strings.ToolSimilarPaths, string.Join(", ", similar));
    }

    private static string Format(string format, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, format, arguments);
}
