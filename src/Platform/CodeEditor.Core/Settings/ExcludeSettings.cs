using CodeEditor.Core.Files;
using Microsoft.Extensions.Options;

namespace CodeEditor.Core.Settings;

/// <summary>
/// Passes <c>files.exclude</c> to the workspace. A separate service because the workspace cannot depend on settings:
/// folder settings are read from the workspace.
/// </summary>
public sealed class ExcludeSettings(IWorkspace workspace, IOptionsMonitor<FilesOptions> options) : IDisposable
{
    private IDisposable? _subscription;
    private string _applied = string.Empty;

    public void Start()
    {
        Apply(options.CurrentValue);
        _subscription ??= options.OnChange(Apply);
    }

    public void Dispose() => _subscription?.Dispose();

    // Settings also change for unrelated keys (font, theme); rescan only when the patterns change.
    private void Apply(FilesOptions files)
    {
        var patterns = files.ExcludedPatterns.Order(StringComparer.Ordinal).ToArray();
        var key = string.Join('\n', patterns);
        if (key == _applied)
        {
            return;
        }

        _applied = key;
        workspace.SetExcludePatterns(GlobFilter.Create(patterns));
    }
}
