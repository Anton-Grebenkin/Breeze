using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;

namespace CodeEditor.Modules.Diagrams.ViewModels;

/// <summary>
/// The file text for the preview: the open document with unsaved edits (edits raise <see cref="Edited"/>) or, while
/// the document is closed, the file on disk. When the whole text is replaced (the file is reopened, or changed on disk
/// while closed by git or another editor) <see cref="Replaced"/> is raised. Events fire on the UI thread.
/// </summary>
internal sealed class PreviewSource : IDisposable
{
    private readonly string _path;
    private readonly DiagramPreviewServices _services;
    private IDocument? _document;

    public PreviewSource(string path, DiagramPreviewServices services)
    {
        _path = path;
        _services = services;
        _services.Documents.Opened += OnOpened;
        _services.Documents.Closed += OnClosed;
        _services.Workspace.FilesChanged += OnFilesChanged;
        if (_services.Documents.TryGet(path, out var document))
        {
            Attach(document);
        }
    }

    /// <summary>The open document's text changed.</summary>
    public event EventHandler? Edited;

    /// <summary>The whole text was replaced: the file was reopened or changed on disk while closed.</summary>
    public event EventHandler? Replaced;

    /// <exception cref="IOException">The closed file could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">No read permission for the closed file.</exception>
    public async Task<string> TextAsync() =>
        _document is not null ? _document.Buffer.GetText() : await _services.Reader.ReadAsync(_path) ?? string.Empty;

    public void Dispose()
    {
        _services.Documents.Opened -= OnOpened;
        _services.Documents.Closed -= OnClosed;
        _services.Workspace.FilesChanged -= OnFilesChanged;
        Detach();
    }

    private void Attach(IDocument document)
    {
        Detach();
        _document = document;
        _document.Buffer.Changed += OnBufferChanged;
    }

    private void Detach()
    {
        if (_document is not null)
        {
            _document.Buffer.Changed -= OnBufferChanged;
            _document = null;
        }
    }

    private void OnBufferChanged(object? sender, EventArgs e) => Edited?.Invoke(this, EventArgs.Empty);

    private void OnOpened(object? sender, IDocument document)
    {
        if (IsOurs(document.FilePath))
        {
            Attach(document);
            Replaced?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnClosed(object? sender, IDocument document)
    {
        if (ReferenceEquals(document, _document))
        {
            Detach();
        }
    }

    // Changes arrive in batches on a background thread; an open document watches the disk itself.
    private void OnFilesChanged(object? sender, FileChangesEventArgs e)
    {
        if (e.RequiresRescan || e.Changes.Any(change => IsOurs(change.Path)))
        {
            _services.Dispatcher.Post(() =>
            {
                if (_document is null)
                {
                    Replaced?.Invoke(this, EventArgs.Empty);
                }
            });
        }
    }

    private bool IsOurs(string path) => string.Equals(Path.GetFullPath(path), _path, StringComparison.OrdinalIgnoreCase);
}
