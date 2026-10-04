using System.Globalization;
using CodeEditor.Core.Context;
using CodeEditor.Core.Resources;
using CodeEditor.Core.Settings;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Files;

/// <summary>
/// Workspace folder: root, exclusions and file watching. Opening is fast: the tree and the index are built lazily
/// by their own modules.
/// </summary>
public sealed partial class Workspace(
    IFileSystem fileSystem,
    IContextKeyService context,
    ILogger<Workspace> logger) : IWorkspace, IDisposable
{
    private PathExclusions _exclusions = PathExclusions.Empty;
    private GlobFilter _userExclusions = GlobFilter.Empty;
    private IFileWatcher? _watcher;

    public string? Root { get; private set; }

    public string? Name => Root is null ? null : Path.GetFileName(Path.TrimEndingDirectorySeparator(Root));

    public event EventHandler? Changed;

    public event EventHandler<FileChangesEventArgs>? FilesChanged;

    public void Open(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        // No trailing slash: "C:\repo\" and "C:\repo" are the same folder (a drive root "C:\" is kept).
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (!fileSystem.DirectoryExists(root))
        {
            throw new DirectoryNotFoundException(string.Format(CultureInfo.CurrentCulture, Strings.FolderNotFound, root));
        }

        CloseCore();
        MigrateLegacyDataFolder(root);
        Root = root;
        _exclusions = PathExclusions.Load(fileSystem, root);
        _watcher = fileSystem.Watch(root, path => IsExcluded(path, isDirectory: false));
        _watcher.Changed += OnFilesChanged;

        context.Set(IWorkspace.OpenContextKey, true);
        LogOpened(logger, root);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Close()
    {
        if (Root is null)
        {
            return;
        }

        CloseCore();
        context.Set(IWorkspace.OpenContextKey, false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetExcludePatterns(GlobFilter patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        _userExclusions = patterns;

        // The tree and index rebuild the same way as after lost watcher events.
        if (Root is not null)
        {
            FilesChanged?.Invoke(this, new FileChangesEventArgs([], requiresRescan: true));
        }
    }

    public bool IsExcluded(string fullPath, bool isDirectory)
    {
        if (Root is null)
        {
            return true;
        }

        var relative = Path.GetRelativePath(Root, fullPath);
        if (relative == ".")
        {
            return false;
        }

        // A path outside the root ("..\") or on another drive (absolute) is not ours.
        return relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
            || _exclusions.IsExcluded(relative, isDirectory)
            || _userExclusions.Matches(relative.Replace(Path.DirectorySeparatorChar, '/'), isDirectory);
    }

    public string RelativePath(string fullPath) =>
        Root is null ? fullPath : Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

    public void Dispose() => CloseCore();

    private void CloseCore()
    {
        if (_watcher is not null)
        {
            _watcher.Changed -= OnFilesChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        Root = null;
        _exclusions = PathExclusions.Empty;
    }

    private void OnFilesChanged(object? sender, FileChangesEventArgs e) => FilesChanged?.Invoke(this, e);

    // The editor folder used to be .codeeditor (before ADR 0038). Rename it before the open events so settings, chats
    // and agent memory stay in place. If another process holds it, the data stays in the old folder.
    private void MigrateLegacyDataFolder(string root)
    {
        var legacy = Path.Combine(root, SettingsService.LegacyWorkspaceFolder);
        var current = Path.Combine(root, SettingsService.WorkspaceFolder);
        if (!fileSystem.DirectoryExists(legacy) || fileSystem.DirectoryExists(current))
        {
            return;
        }

        try
        {
            fileSystem.Move(legacy, current);
            LogDataFolderMigrated(logger, legacy, current);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogDataFolderMigrationFailed(logger, legacy, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Opened folder {Root}")]
    private static partial void LogOpened(ILogger logger, string root);

    [LoggerMessage(Level = LogLevel.Information, Message = "Renamed editor data folder {Legacy} to {Current}")]
    private static partial void LogDataFolderMigrated(ILogger logger, string legacy, string current);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not rename editor data folder {Legacy}")]
    private static partial void LogDataFolderMigrationFailed(ILogger logger, string legacy, Exception exception);
}
