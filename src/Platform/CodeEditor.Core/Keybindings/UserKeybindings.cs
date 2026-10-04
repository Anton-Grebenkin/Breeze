using System.Globalization;
using System.Text;
using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Core.Resources;
using CodeEditor.Core.Storage;
using CodeEditor.Core.Threading;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Keybindings;

/// <summary>
/// User keybindings from <c>keybindings.json</c> in VS Code format: an array of rules
/// <c>{ "key": "ctrl+shift+d", "command": "id", "when": "editorFocus", "args": 5 }</c>; <c>"-id"</c> removes a
/// default binding. Rules are registered after modules, so they take precedence; the file reloads on change.
/// Invalid rules are skipped, the rest still apply.
/// </summary>
public sealed partial class UserKeybindings : IDisposable
{
    public const string FileName = "keybindings.json";

    private const string TemplateExamples = "// { \"key\": \"ctrl+shift+d\", \"command\": \"workbench.action.toggleTheme\" },\n// { \"key\": \"ctrl+k ctrl+t\", \"command\": \"-workbench.action.toggleTheme\" }\n[\n]\n";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IKeybindingRegistry _registry;
    private readonly IFileSystem _fileSystem;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<UserKeybindings> _logger;
    private readonly List<IDisposable> _registrations = [];
    private IFileWatcher? _watcher;

    public UserKeybindings(IKeybindingRegistry registry, IFileSystem fileSystem, UserDataPaths paths, IUiDispatcher dispatcher, ILogger<UserKeybindings> logger)
    {
        _registry = registry;
        _fileSystem = fileSystem;
        _dispatcher = dispatcher;
        _logger = logger;
        FilePath = paths.File(FileName);
    }

    public string FilePath { get; }

    /// <summary>Errors from the last read: a corrupt file or invalid rules.</summary>
    public IReadOnlyList<string> Errors { get; private set; } = [];

    public event EventHandler? Changed;

    /// <summary>Reads the file and starts watching it. Call after modules have registered their commands.</summary>
    public void Start()
    {
        Reload();
        var folder = Path.GetDirectoryName(FilePath)!;
        try
        {
            var fullPath = Path.GetFullPath(FilePath);
            _watcher = _fileSystem.Watch(
                folder,
                path => !string.Equals(Path.GetFullPath(path), fullPath, StringComparison.OrdinalIgnoreCase));
            _watcher.Changed += (_, _) => _dispatcher.Post(Reload);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            LogReadFailed(_logger, FilePath, exception.Message);
        }
    }

    public string EnsureFile()
    {
        if (!_fileSystem.FileExists(FilePath))
        {
            _fileSystem.WriteAllBytesAtomic(FilePath, Encoding.UTF8.GetBytes($"// {Strings.KeybindingsTemplateHeader}\n{TemplateExamples}"));
        }

        return FilePath;
    }

    public void Reload()
    {
        Unregister();
        var errors = new List<string>();
        try
        {
            if (_fileSystem.FileExists(FilePath))
            {
                Apply(_fileSystem.ReadAllText(FilePath), errors);
            }
        }
        catch (JsonException exception)
        {
            errors.Add(string.Format(CultureInfo.CurrentCulture, Strings.FileError, FileName, exception.Message));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogReadFailed(_logger, FilePath, exception.Message);
        }

        Errors = errors;
        foreach (var error in errors)
        {
            LogInvalidRule(_logger, error);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        Unregister();
    }

    private void Apply(string json, List<string> errors)
    {
        using var document = JsonDocument.Parse(json, DocumentOptions);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException(Strings.KeybindingsNotArray);
        }

        // Removals first, so new bindings of the same command are not hidden by its "-command" rule.
        var rules = document.RootElement.EnumerateArray().Select(KeybindingRule.Read).ToArray();
        foreach (var rule in rules.Where(rule => rule.Error is null && rule.IsRemoval))
        {
            _registrations.Add(_registry.Suppress(rule.CommandId, rule.Sequence));
        }

        foreach (var rule in rules.Where(rule => rule.Error is null && !rule.IsRemoval))
        {
            _registrations.Add(_registry.Register(new KeybindingDefinition(rule.Sequence!.Value, rule.CommandId, rule.When, rule.Argument)));
        }

        errors.AddRange(rules.Select(rule => rule.Error).OfType<string>());
    }

    private void Unregister()
    {
        // Reverse order: drop our bindings first, then restore the hidden defaults.
        for (var i = _registrations.Count - 1; i >= 0; i--)
        {
            _registrations[i].Dispose();
        }

        _registrations.Clear();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Key bindings: {Error}")]
    private static partial void LogInvalidRule(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read {Path}: {Reason}")]
    private static partial void LogReadFailed(ILogger logger, string path, string reason);
}
