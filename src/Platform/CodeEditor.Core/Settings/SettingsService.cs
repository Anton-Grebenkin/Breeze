using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Core.Resources;
using CodeEditor.Core.Storage;
using CodeEditor.Core.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Settings;

/// <inheritdoc cref="ISettingsService"/>
public sealed partial class SettingsService : ISettingsService, IDisposable
{
    public const string FileName = "settings.json";
    public const string WorkspaceFolder = ".breeze";

    /// <summary>Editor folder name before ADR 0038; <see cref="Workspace"/> renames it.</summary>
    public const string LegacyWorkspaceFolder = ".codeeditor";

    private const string TemplateExamples = "// \"editor.fontSize\": 16,\n// \"workbench.colorTheme\": \"Light+\"\n{\n}\n";

    private readonly IFileSystem _fileSystem;
    private readonly IWorkspace _workspace;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<SettingsService> _logger;
    private readonly SettingsProvider _user = new();
    private readonly SettingsProvider _folder = new();
    private readonly ConfigurationRoot _configuration;
    private readonly IFileWatcher? _userWatcher;
    private string? _userError;
    private string? _folderError;

    public SettingsService(IFileSystem fileSystem, UserDataPaths paths, IWorkspace workspace, IUiDispatcher dispatcher, ILogger<SettingsService> logger)
    {
        _fileSystem = fileSystem;
        _workspace = workspace;
        _dispatcher = dispatcher;
        _logger = logger;
        UserSettingsPath = paths.File(FileName);

        // Layer order matters: folder settings override user settings.
        _configuration = new ConfigurationRoot([_user, _folder]);
        _userError = Load(_user, UserSettingsPath);

        try
        {
            if (!_fileSystem.DirectoryExists(paths.Root))
            {
                _fileSystem.CreateDirectory(paths.Root);
            }

            _userWatcher = _fileSystem.Watch(paths.Root, path => !IsSame(path, UserSettingsPath));
            _userWatcher.Changed += (_, _) => _dispatcher.Post(OnUserFileChanged);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogWatchFailed(_logger, paths.Root, exception);
        }

        _workspace.Changed += OnWorkspaceChanged;
        _workspace.FilesChanged += OnWorkspaceFilesChanged;
    }

    public event EventHandler? Changed;

    public IConfiguration Configuration => _configuration;

    public string UserSettingsPath { get; }

    public string? WorkspaceSettingsPath =>
        _workspace.Root is { } root ? Path.Combine(root, WorkspaceFolder, FileName) : null;

    public string? Error => _userError ?? _folderError;

    public bool TrySetUserValue(string key, object? value, [NotNullWhen(false)] out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var text = _fileSystem.FileExists(UserSettingsPath) ? _fileSystem.ReadAllText(UserSettingsPath) : string.Empty;
            var updated = value is null ? SettingsJson.RemoveValue(text, key) : SettingsJson.SetValue(text, key, value);
            _fileSystem.WriteAllBytesAtomic(UserSettingsPath, Encoding.UTF8.GetBytes(updated));
        }
        catch (JsonException exception)
        {
            error = string.Format(CultureInfo.CurrentCulture, Strings.SettingsWriteFailedFix, FileName, exception.Message);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = string.Format(CultureInfo.CurrentCulture, Strings.SettingsWriteFailed, exception.Message);
            return false;
        }

        // Do not wait for the file watcher: the value applies immediately.
        LogValueSet(_logger, key, value ?? "(default)");
        ReloadUser();
        error = null;
        return true;
    }

    public string EnsureUserSettingsFile()
    {
        if (!_fileSystem.FileExists(UserSettingsPath))
        {
            _fileSystem.WriteAllBytesAtomic(UserSettingsPath, Template());
        }

        return UserSettingsPath;
    }

    public string? EnsureWorkspaceSettingsFile()
    {
        if (WorkspaceSettingsPath is not { } path)
        {
            return null;
        }

        if (!_fileSystem.FileExists(path))
        {
            var folder = Path.GetDirectoryName(path)!;
            if (!_fileSystem.DirectoryExists(folder))
            {
                _fileSystem.CreateDirectory(folder);
            }

            _fileSystem.WriteAllBytesAtomic(path, Template());
        }

        return path;
    }

    public void Dispose()
    {
        _userWatcher?.Dispose();
        _workspace.Changed -= OnWorkspaceChanged;
        _workspace.FilesChanged -= OnWorkspaceFilesChanged;
        _configuration.Dispose();
    }

    private void ReloadUser()
    {
        _userError = Load(_user, UserSettingsPath);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnUserFileChanged()
    {
        LogReloaded(_logger, UserSettingsPath);
        ReloadUser();
    }

    private void ReloadFolder()
    {
        _folderError = Load(_folder, WorkspaceSettingsPath);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnWorkspaceChanged(object? sender, EventArgs e) => ReloadFolder();

    private void OnWorkspaceFilesChanged(object? sender, FileChangesEventArgs e)
    {
        if (WorkspaceSettingsPath is not { } path)
        {
            return;
        }

        var folder = Path.GetDirectoryName(path)!;
        if (e.RequiresRescan || e.Changes.Any(change => IsSame(change.Path, path) || IsSame(change.Path, folder)))
        {
            _dispatcher.Post(ReloadFolder);
        }
    }

    /// <summary>Loads a layer; a parse error keeps the previous values and returns a user-facing message.</summary>
    private string? Load(SettingsProvider provider, string? path)
    {
        try
        {
            provider.Replace(path is not null && _fileSystem.FileExists(path) ? SettingsJson.Parse(_fileSystem.ReadAllText(path)) : []);
            return null;
        }
        catch (JsonException exception)
        {
            LogInvalidSettings(_logger, path!, exception.Message);
            return string.Format(CultureInfo.CurrentCulture, Strings.FileError, path, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Another process is writing the file; it is read again on the next change.
            LogInvalidSettings(_logger, path!, exception.Message);
            return null;
        }
    }

    private static bool IsSame(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static byte[] Template() =>
        Encoding.UTF8.GetBytes($"// {Strings.SettingsTemplateHeader}\n{TemplateExamples}");

    [LoggerMessage(Level = LogLevel.Information, Message = "Setting {Key} = {Value}")]
    private static partial void LogValueSet(ILogger logger, string key, object value);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Settings {Path} changed on disk, reloaded")]
    private static partial void LogReloaded(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Settings {Path} could not be read: {Reason}")]
    private static partial void LogInvalidSettings(ILogger logger, string path, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Settings watching in {Folder} is unavailable")]
    private static partial void LogWatchFailed(ILogger logger, string folder, Exception exception);
}
