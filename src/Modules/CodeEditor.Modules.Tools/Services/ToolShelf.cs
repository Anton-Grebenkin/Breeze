using System.Collections.Immutable;
using CodeEditor.Core.Files;
using CodeEditor.Core.Settings;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Tools.Services;

/// <summary>
/// Tools of the open folder (<c>.breeze/tools/&lt;name&gt;/tool.md</c>) and personal tools from the user data folder;
/// a folder tool wins over a personal one with the same name. Rescans when the folder or its tools change.
/// </summary>
public sealed partial class ToolShelf : IDisposable
{
    public const string FolderName = "tools";

    /// <summary>Tools folder relative to the workspace root.</summary>
    public const string WorkspaceFolder = SettingsService.WorkspaceFolder + "/" + FolderName;

    private const int MaxTools = 100;

    private readonly IWorkspace _workspace;
    private readonly IFileSystem _fileSystem;
    private readonly UserDataPaths _paths;
    private readonly ILogger<ToolShelf> _logger;
    private readonly Lock _lock = new();
    private ImmutableArray<ToolDefinition> _tools = [];
    private ImmutableArray<string> _problems = [];
    private bool _scanned;

    public ToolShelf(IWorkspace workspace, IFileSystem fileSystem, UserDataPaths paths, ILogger<ToolShelf> logger)
    {
        _workspace = workspace;
        _fileSystem = fileSystem;
        _paths = paths;
        _logger = logger;
        _workspace.Changed += OnWorkspaceChanged;
        _workspace.FilesChanged += OnFilesChanged;
    }

    /// <summary>The list of tools changed; may come from a background thread.</summary>
    public event EventHandler? Changed;

    public ImmutableArray<ToolDefinition> Tools
    {
        get
        {
            EnsureScanned();
            return _tools;
        }
    }

    /// <summary>Problems in tool folders from the last scan.</summary>
    public ImmutableArray<string> Problems
    {
        get
        {
            EnsureScanned();
            return _problems;
        }
    }

    /// <summary>Personal tools folder.</summary>
    public string PersonalFolder => _paths.File(FolderName);

    public ToolDefinition? Find(string name) => Tools.FirstOrDefault(tool => tool.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Scans the folders again; <see cref="Changed"/> comes only if the tools differ.</summary>
    public void Refresh()
    {
        var problems = new List<string>();
        var tools = Scan(problems);
        bool changed;
        lock (_lock)
        {
            changed = !_scanned || Fingerprint(tools) != Fingerprint(_tools);
            _tools = tools;
            _problems = [.. problems];
            _scanned = true;
        }

        foreach (var problem in problems)
        {
            LogProblem(_logger, problem);
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _workspace.Changed -= OnWorkspaceChanged;
        _workspace.FilesChanged -= OnFilesChanged;
    }

    private void EnsureScanned()
    {
        if (!_scanned)
        {
            Refresh();
        }
    }

    private ImmutableArray<ToolDefinition> Scan(List<string> problems)
    {
        var folders = new List<(string Folder, bool Personal)>();
        if (_workspace.Root is { } root)
        {
            folders.AddRange(ToolFolders(Path.Combine(root, SettingsService.WorkspaceFolder, FolderName)).Select(folder => (folder, false)));
        }

        folders.AddRange(ToolFolders(PersonalFolder).Select(folder => (folder, true)));
        var tools = ImmutableArray.CreateBuilder<ToolDefinition>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (folder, personal) in folders.Take(MaxTools))
        {
            if (Read(folder, personal, problems) is { } tool && names.Add(tool.Name))
            {
                tools.Add(tool);
            }
        }

        return tools.ToImmutable();
    }

    private ToolDefinition? Read(string folder, bool personal, List<string> problems)
    {
        try
        {
            return ToolDefinitionReader.Read(_fileSystem, folder, personal, problems);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add($"{Path.GetFileName(folder)}: {exception.Message}");
            return null;
        }
    }

    private IEnumerable<string> ToolFolders(string shelf)
    {
        if (!_fileSystem.DirectoryExists(shelf))
        {
            return [];
        }

        return _fileSystem.EnumerateEntries(shelf)
            .Where(entry => entry.IsDirectory && _fileSystem.FileExists(Path.Combine(entry.FullPath, ToolDefinition.DefinitionFile)))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.FullPath);
    }

    // Records hold parameter lists by reference: compare what users and the agent see.
    private static string Fingerprint(ImmutableArray<ToolDefinition> tools) =>
        string.Join('\n', tools.Select(tool => $"{tool.Name}|{tool.Description}|{tool.Script}|{tool.Timeout}|{string.Join(";", tool.Parameters)}"));

    private void OnWorkspaceChanged(object? sender, EventArgs e) => Refresh();

    private void OnFilesChanged(object? sender, FileChangesEventArgs e)
    {
        if (e.RequiresRescan || e.Changes.Any(change => IsShelfPath(change.Path)))
        {
            Refresh();
        }
    }

    private bool IsShelfPath(string path) =>
        _workspace.RelativePath(path).StartsWith(WorkspaceFolder, StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool shelf: {Problem}")]
    private static partial void LogProblem(ILogger logger, string problem);
}
