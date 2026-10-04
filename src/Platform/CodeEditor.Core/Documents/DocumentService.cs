using System.Diagnostics;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Core.Resources;
using CodeEditor.Core.Threading;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Documents;

/// <summary>
/// Open documents. Reading, encoding detection and writing run in the background; buffers are created and changed
/// on the UI thread. On an external change a clean document reloads silently (like VS Code); a dirty one is
/// flagged as a conflict for the user to resolve.
/// </summary>
public sealed partial class DocumentService : IDocumentService, IDisposable
{
    /// <summary>Larger files are not opened in the text editor; leaves ample headroom over the 10 MB budget.</summary>
    public const long MaxFileSize = 64L * 1024 * 1024;

    private readonly IFileSystem _fileSystem;
    private readonly ITextBufferFactory _buffers;
    private readonly IUiDispatcher _dispatcher;
    private readonly IWorkspace _workspace;
    private readonly ILogger<DocumentService> _logger;
    private readonly Dictionary<string, Document> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<IDocument>> _opening = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IDocument> _documents = [];

    public DocumentService(
        IFileSystem fileSystem,
        ITextBufferFactory buffers,
        IUiDispatcher dispatcher,
        IWorkspace workspace,
        ILogger<DocumentService> logger)
    {
        _fileSystem = fileSystem;
        _buffers = buffers;
        _dispatcher = dispatcher;
        _workspace = workspace;
        _logger = logger;
        _workspace.FilesChanged += OnFilesChanged;
    }

    public IReadOnlyList<IDocument> Documents => _documents;

    public event EventHandler<IDocument>? Opened;

    public event EventHandler<IDocument>? Closed;

    public event EventHandler<IDocument>? Saved;

    public bool TryGet(string filePath, out IDocument document)
    {
        var found = _byPath.TryGetValue(Path.GetFullPath(filePath), out var existing);
        document = existing!;
        return found;
    }

    public Task<IDocument> OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var path = Path.GetFullPath(filePath);
        if (_byPath.TryGetValue(path, out var existing))
        {
            return Task.FromResult<IDocument>(existing);
        }

        // A repeated request while the file is loading gets the same task, not a second document.
        if (!_opening.TryGetValue(path, out var opening))
        {
            opening = OpenCoreAsync(path, cancellationToken);
            _opening[path] = opening;
        }

        return opening;
    }

    public async Task SaveAsync(IDocument document, CancellationToken cancellationToken = default)
    {
        var target = (Document)document;
        var text = target.Buffer.GetText();

        await Task.Run(() =>
        {
            _fileSystem.WriteAllBytesAtomic(target.FilePath, TextFileCodec.Encode(text, target.Format));
            target.LastWriteTimeUtc = _fileSystem.GetLastWriteTimeUtc(target.FilePath);
        }, cancellationToken).ConfigureAwait(false);

        await _dispatcher.InvokeAsync(() =>
        {
            target.Buffer.MarkSaved();
            target.SetExternalState(hasExternalChanges: false, isDeletedOnDisk: false);
            LogSaved(_logger, target.FilePath);
            Saved?.Invoke(this, target);
        }).ConfigureAwait(false);
    }

    public async Task SaveAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var document in _documents.Where(static document => document.IsDirty).ToArray())
        {
            await SaveAsync(document, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ReloadAsync(IDocument document, CancellationToken cancellationToken = default)
    {
        var target = (Document)document;
        var (text, _, lastWrite) = await ReadAsync(target.FilePath, cancellationToken).ConfigureAwait(false);

        await _dispatcher.InvokeAsync(() =>
        {
            target.Buffer.Reset(text);
            target.Buffer.MarkSaved();
            target.LastWriteTimeUtc = lastWrite;
            target.SetExternalState(hasExternalChanges: false, isDeletedOnDisk: false);
        }).ConfigureAwait(false);
    }

    public void Close(IDocument document)
    {
        var target = (Document)document;
        if (_byPath.Remove(target.FilePath))
        {
            _documents.Remove(target);
            target.Detach();
            Closed?.Invoke(this, target);
        }
    }

    public void Dispose() => _workspace.FilesChanged -= OnFilesChanged;

    private async Task<IDocument> OpenCoreAsync(string path, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var (text, format, lastWrite) = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
            var buffer = await Task.Run(() => _buffers.Create(text), cancellationToken).ConfigureAwait(false);

            IDocument? result = null;
            var uiTime = TimeSpan.Zero;
            await _dispatcher.InvokeAsync(() =>
            {
                var uiStarted = Stopwatch.GetTimestamp();
                var document = new Document(path, buffer, format, lastWrite);
                _byPath[path] = document;
                _documents.Add(document);
                Opened?.Invoke(this, document);
                result = document;
                uiTime = Stopwatch.GetElapsedTime(uiStarted);
            }).ConfigureAwait(false);

            // Budget: 10 MB in at most 300 ms; the UI thread only registers the ready document.
            LogOpened(_logger, path, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, (long)uiTime.TotalMilliseconds);
            return result!;
        }
        finally
        {
            await _dispatcher.InvokeAsync(() => _opening.Remove(path)).ConfigureAwait(false);
        }
    }

    private Task<(string Text, TextFileFormat Format, DateTime LastWrite)> ReadAsync(string path, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            try
            {
                if (_fileSystem.GetFileLength(path) > MaxFileSize)
                {
                    throw new DocumentOpenException(Format(Strings.DocumentTooLarge, Path.GetFileName(path), MaxFileSize / (1024 * 1024)), DocumentOpenFailure.TooLarge);
                }

                var lastWrite = _fileSystem.GetLastWriteTimeUtc(path);
                var decoded = TextFileCodec.Decode(_fileSystem.ReadAllBytes(path))
                    ?? throw new DocumentOpenException(Format(Strings.DocumentBinary, Path.GetFileName(path)), DocumentOpenFailure.Binary);
                return (decoded.Text, decoded.Format, lastWrite);
            }
            catch (Exception exception) when (exception is (IOException or UnauthorizedAccessException) and not DocumentOpenException)
            {
                throw new DocumentOpenException(Format(Strings.DocumentReadFailed, Path.GetFileName(path), exception.Message), exception);
            }
        }, cancellationToken);

    private static string Format(string template, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, template, args);

    private void OnFilesChanged(object? sender, FileChangesEventArgs e) =>
        _dispatcher.Post(() =>
        {
            foreach (var change in e.Changes)
            {
                if (_byPath.TryGetValue(change.Path, out var document))
                {
                    _ = HandleExternalChangeAsync(document, change.Kind);
                }
            }
        });

    private async Task HandleExternalChangeAsync(Document document, FileChangeKind kind)
    {
        if (kind == FileChangeKind.Deleted || !_fileSystem.FileExists(document.FilePath))
        {
            document.SetExternalState(document.HasExternalChanges, isDeletedOnDisk: true);
            return;
        }

        if (_fileSystem.GetLastWriteTimeUtc(document.FilePath) == document.LastWriteTimeUtc)
        {
            // Our own save or a "change" without new content.
            document.SetExternalState(document.HasExternalChanges, isDeletedOnDisk: false);
            return;
        }

        if (document.IsDirty)
        {
            document.SetExternalState(hasExternalChanges: true, isDeletedOnDisk: false);
            return;
        }

        try
        {
            await ReloadAsync(document).ConfigureAwait(false);
        }
        catch (DocumentOpenException exception)
        {
            LogReloadFailed(_logger, exception, document.FilePath);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Opened {Path} in {TotalMs} ms, {UiMs} ms of it on the UI thread")]
    private static partial void LogOpened(ILogger logger, string path, long totalMs, long uiMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved {Path}")]
    private static partial void LogSaved(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not reload file changed on disk: {Path}")]
    private static partial void LogReloadFailed(ILogger logger, Exception exception, string path);
}
