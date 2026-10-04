using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Settings;
using CodeEditor.Core.Threading;
using Microsoft.Extensions.Options;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Auto save, like <c>files.autoSave</c> in VS Code: <c>afterDelay</c> saves after a pause following the last edit,
/// <c>onFocusChange</c> when focus leaves the text. Files changed or deleted on disk are skipped: the user resolves
/// the conflict.
/// </summary>
public sealed class AutoSaveService : IDisposable
{
    private readonly IDocumentService _documents;
    private readonly DocumentSaver _saver;
    private readonly IContextKeyService _context;
    private readonly IOptionsMonitor<FilesOptions> _options;
    private readonly IUiDispatcher _dispatcher;
    private readonly ITimer _timer;
    private readonly IDisposable? _subscription;

    public AutoSaveService(
        IDocumentService documents,
        DocumentSaver saver,
        IContextKeyService context,
        IOptionsMonitor<FilesOptions> options,
        IUiDispatcher dispatcher,
        TimeProvider time)
    {
        _documents = documents;
        _saver = saver;
        _context = context;
        _options = options;
        _dispatcher = dispatcher;
        _timer = time.CreateTimer(_ => _dispatcher.Post(() => _ = SaveDirtyAsync()), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        foreach (var document in documents.Documents)
        {
            document.Buffer.Changed += OnBufferChanged;
        }

        _documents.Opened += OnOpened;
        _documents.Closed += OnClosed;
        _context.Changed += OnContextChanged;
        // Any settings write reloads the options (zoom, theme): a pending save is dropped only if the mode changed.
        _subscription = options.OnChange(changed =>
        {
            if (changed.AutoSave != FilesOptions.AutoSaveAfterDelay)
            {
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        });
    }

    private string Mode => _options.CurrentValue.AutoSave;

    public void Dispose()
    {
        _documents.Opened -= OnOpened;
        _documents.Closed -= OnClosed;
        _context.Changed -= OnContextChanged;
        foreach (var document in _documents.Documents)
        {
            document.Buffer.Changed -= OnBufferChanged;
        }

        _subscription?.Dispose();
        _timer.Dispose();
    }

    /// <summary>Saves all dirty documents that don't conflict with the disk.</summary>
    public async Task SaveDirtyAsync()
    {
        foreach (var document in _documents.Documents.Where(CanAutoSave).ToArray())
        {
            await _saver.SaveAsync(document);
        }
    }

    private static bool CanAutoSave(IDocument document) =>
        document.IsDirty && !document.HasExternalChanges && !document.IsDeletedOnDisk;

    private void OnOpened(object? sender, IDocument document) => document.Buffer.Changed += OnBufferChanged;

    private void OnClosed(object? sender, IDocument document) => document.Buffer.Changed -= OnBufferChanged;

    // Every edit restarts the delay, so saving happens once the user pauses.
    private void OnBufferChanged(object? sender, EventArgs e)
    {
        if (Mode == FilesOptions.AutoSaveAfterDelay)
        {
            _timer.Change(TimeSpan.FromMilliseconds(Math.Max(0, _options.CurrentValue.AutoSaveDelay)), Timeout.InfiniteTimeSpan);
        }
    }

    private void OnContextChanged(object? sender, ContextKeyChangedEventArgs e)
    {
        if (Mode == FilesOptions.AutoSaveOnFocusChange
            && e.Key == EditorContextKeys.TextFocus
            && _context.GetValue(EditorContextKeys.TextFocus) is not true)
        {
            _ = SaveDirtyAsync();
        }
    }
}
