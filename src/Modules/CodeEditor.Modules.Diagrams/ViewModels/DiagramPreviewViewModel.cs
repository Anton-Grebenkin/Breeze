using System.Collections.Immutable;
using System.Globalization;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Theming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Diagrams.ViewModels;

/// <summary>
/// Live preview of a file's diagrams (ADR 0035), an editor tab beside the text. Edits re-render the diagrams
/// <see cref="TypingDelay"/> after the last keystroke; a parse error is shown as text with the file line while the image
/// stays the last successful one (<see cref="PreviewPictures"/>). Markdown shows all <c>```mermaid</c> blocks in order;
/// unchanged blocks are not re-rendered (<see cref="PreviewRenderCache"/>). Diagrams follow the editor theme. When the
/// document is closed the preview keeps the last text and comes alive again when the file is reopened
/// (<see cref="PreviewSource"/>). Zoom and panning live on the view page.
/// </summary>
public sealed partial class DiagramPreviewViewModel : ObservableObject, IDisposable
{
    /// <summary>Pause after an edit before re-rendering, so not every keystroke renders.</summary>
    public static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(300);

    private readonly DiagramPreviewServices _services;
    private readonly PreviewSource _source;
    private readonly PreviewRenderCache _renders;
    private readonly PreviewPictures _pictures;
    private CancellationTokenSource? _run;

    public DiagramPreviewViewModel(string filePath, DiagramPreviewServices services)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(services);
        FilePath = Path.GetFullPath(filePath);
        _services = services;
        _renders = new PreviewRenderCache(services.Renderer);
        _pictures = new PreviewPictures(Caption);
        _source = new PreviewSource(FilePath, services);
        _source.Edited += OnEdited;
        _source.Replaced += OnRedrawNeeded;
        services.Themes.Changed += OnRedrawNeeded;
        Theme = ThemeOf(services.Themes.Current);
        Schedule(TimeSpan.Zero);
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    /// <summary>Rendered diagrams in order; a diagram that never rendered is not included.</summary>
    [ObservableProperty]
    public partial ImmutableArray<DiagramPreviewItem> Diagrams { get; private set; } = [];

    /// <summary>The theme <see cref="Diagrams"/> were rendered in; the page background matches it.</summary>
    [ObservableProperty]
    public partial DiagramTheme Theme { get; private set; }

    /// <summary>Diagram errors: "Line 14: Parse error:" and the fragment; <c>null</c> if there are none.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>The file line of the first error, for navigating to it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoToErrorCommand))]
    public partial int? ErrorLine { get; private set; }

    /// <summary>
    /// Text shown instead of diagrams: rendering in progress, no diagrams, or none rendered; <c>null</c> when diagrams
    /// are visible.
    /// </summary>
    [ObservableProperty]
    public partial string? Placeholder { get; private set; } = Strings.PreviewRendering;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    public partial double Zoom { get; private set; } = 1;

    /// <summary>The zoom for the button, e.g. "125 %".</summary>
    public string ZoomText => Zoom.ToString("P0", CultureInfo.CurrentCulture);

    /// <summary>The tab was closed: the view should release the page.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    /// Fit to window: the page computes the zoom (it knows the window and diagram sizes) and reports it via
    /// <see cref="ReportZoom"/>.
    /// </summary>
    public event EventHandler? FitRequested;

    /// <summary>
    /// Rendering finished, successfully or not: <see cref="Diagrams"/>, <see cref="Theme"/>, <see cref="Error"/> and
    /// <see cref="Placeholder"/> are consistent, and the view shows them at once.
    /// </summary>
    public event EventHandler? Rendered;

    /// <summary>The most recently started render; tests await it.</summary>
    internal Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>The page changed the zoom: <c>Ctrl</c>+wheel or fit.</summary>
    public void ReportZoom(double zoom) => Zoom = DiagramZoom.Clamp(zoom);

    /// <summary>Page keys + − 0 (<see cref="DiagramPageMessages"/>), with the same zoom steps as the buttons.</summary>
    public void PressKey(string? key)
    {
        switch (key)
        {
            case DiagramPageMessages.ZoomInKey:
                ZoomIn();
                break;
            case DiagramPageMessages.ZoomOutKey:
                ZoomOut();
                break;
            case DiagramPageMessages.ZoomResetKey:
                ResetZoom();
                break;
            default:
                break;
        }
    }

    /// <summary>The text the preview shows: the document with unsaved edits or the file from disk.</summary>
    public Task<string> TextAsync() => _source.TextAsync();

    public void Dispose()
    {
        IsClosed = true;
        _source.Edited -= OnEdited;
        _source.Replaced -= OnRedrawNeeded;
        _services.Themes.Changed -= OnRedrawNeeded;
        _source.Dispose();
        _run?.Cancel();
        _run?.Dispose();
        _run = null;
    }

    [RelayCommand]
    private void ZoomIn() => Zoom = DiagramZoom.In(Zoom);

    [RelayCommand]
    private void ZoomOut() => Zoom = DiagramZoom.Out(Zoom);

    [RelayCommand]
    private void ResetZoom() => Zoom = 1;

    [RelayCommand]
    private void Fit() => FitRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private Task ExportSvgAsync() => _services.Exports.ExportAsync(FilePath, TextAsync, DiagramFormat.Svg);

    [RelayCommand]
    private Task ExportPngAsync() => _services.Exports.ExportAsync(FilePath, TextAsync, DiagramFormat.Png);

    /// <summary>Opens the file and puts the caret on the first error line.</summary>
    [RelayCommand(CanExecute = nameof(CanGoToError))]
    private async Task GoToErrorAsync()
    {
        if (ErrorLine is not { } line)
        {
            return;
        }

        await _services.Commands.ExecuteAsync(ShellCommandIds.OpenFile, FilePath);
        await _services.Commands.ExecuteAsync(ShellCommandIds.EditorGoToLine, line);
    }

    private bool CanGoToError() => ErrorLine is not null;

    private void OnEdited(object? sender, EventArgs e) => Schedule(TypingDelay);

    // The theme changed, or the file was reopened or changed on disk: re-render immediately.
    private void OnRedrawNeeded(object? sender, EventArgs e) => Schedule(TimeSpan.Zero);

    private void Schedule(TimeSpan delay)
    {
        _run?.Cancel();
        _run?.Dispose();
        var run = _run = new CancellationTokenSource();
        Pending = RenderAsync(delay, run.Token);
    }

    private async Task RenderAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, _services.Time, cancellationToken);
            }

            var theme = ThemeOf(_services.Themes.Current);
            var text = await TextAsync();

            // Parse large Markdown in the background: the UI thread only reads the buffer and shows images.
            var diagrams = await Task.Run(() => DiagramFiles.Extract(FilePath, text), cancellationToken);
            var results = await _renders.RenderAsync(diagrams, theme, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Show(diagrams, results, theme);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The text changed or the tab closed; the next render replaces the image.
        }
        catch (DiagramRendererException exception)
        {
            ShowFailure(exception.Message, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowFailure(string.Format(CultureInfo.CurrentCulture, Strings.ReadFailed, exception.Message), cancellationToken);
        }
    }

    private void Show(IReadOnlyList<DiagramSource> diagrams, IReadOnlyList<DiagramResult<string>> results, DiagramTheme theme)
    {
        var (items, problems) = _pictures.Update(diagrams, results);
        Theme = theme;
        Diagrams = items;
        Error = problems.Count == 0 ? null : string.Join("\n\n", problems.Select(DiagramErrors.Describe));
        ErrorLine = problems.Count == 0 ? null : problems[0].Line;
        Placeholder = diagrams.Count switch
        {
            0 => DiagramFiles.IsMarkdown(FilePath) ? Strings.PreviewNoBlocks : Strings.PreviewEmpty,
            _ when items.IsEmpty => Strings.PreviewNotRendered,
            _ => null,
        };
        Rendered?.Invoke(this, EventArgs.Empty);
    }

    // The renderer does not work (no network and no local Mermaid copy) or the file could not be read: the error strip
    // shows the reason and the previous diagrams stay.
    private void ShowFailure(string message, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        Error = message;
        ErrorLine = null;
        Placeholder = Diagrams.IsEmpty ? Strings.PreviewNotRendered : null;
        Rendered?.Invoke(this, EventArgs.Empty);
    }

    private string? Caption(DiagramSource diagram) =>
        DiagramFiles.IsMarkdown(FilePath) ? string.Format(CultureInfo.CurrentCulture, Strings.PreviewCaption, diagram.Index, diagram.FirstLine) : null;

    private static DiagramTheme ThemeOf(ThemeKind theme) => theme == ThemeKind.Light ? DiagramTheme.Light : DiagramTheme.Dark;
}
