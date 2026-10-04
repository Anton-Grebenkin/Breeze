using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Text;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>
/// Files the agent changed in this chat, above the input box: "3 files changed +12 −4"; each can be opened, diffed or
/// reverted. Accept saves the files' tabs and restarts the diff; Revert all restores the original text (into the tab,
/// undoable with <c>Ctrl+Z</c>). The buttons are disabled while the agent works: a revert mid-turn would break the
/// file versions it knows. The list also updates when the user accepts or rejects hunks in the editor.
/// </summary>
public sealed partial class ChangesPanelViewModel : ObservableObject, IDisposable
{
    private readonly ChatChanges _changes;
    private readonly AgentFileState _fileState;
    private readonly IAgentChangeReverter _reverter;
    private readonly IDocumentService _documents;
    private readonly IUiDispatcher _dispatcher;
    private readonly ICommandService _commands;
    private readonly StatusBarViewModel _statusBar;

    public ChangesPanelViewModel(
        ChatChanges changes,
        AgentFileState fileState,
        IAgentChangeReverter reverter,
        IDocumentService documents,
        IUiDispatcher dispatcher,
        ICommandService commands,
        StatusBarViewModel statusBar)
    {
        _changes = changes;
        _fileState = fileState;
        _reverter = reverter;
        _documents = documents;
        _dispatcher = dispatcher;
        _commands = commands;
        _statusBar = statusBar;
        _fileState.ChangesChanged += OnChangesChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiles), nameof(Title), nameof(Counts))]
    public partial IReadOnlyList<ChangedFileViewModel> Files { get; private set; } = [];

    public bool HasFiles => Files.Count > 0;

    public string Title => Format(Strings.ChangedFilesTitle, Files.Count);

    public string Counts => $"+{Files.Sum(file => file.Diff.Added)} −{Files.Sum(file => file.Diff.Removed)}";

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand), nameof(RevertAllCommand), nameof(RevertFileCommand))]
    public partial bool IsAgentBusy { get; set; }

    /// <summary>Reloads the chat's changes: after a turn, on chat switch and after decisions in the editor.</summary>
    public async Task RefreshAsync()
    {
        try
        {
            var collected = await _changes.CollectAsync();
            Files = [.. collected.Select(change => new ChangedFileViewModel(change))];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file is locked by another process; keep the old list until the next turn.
            _statusBar.Message = Format(Strings.ChangedFilesReadFailed, exception.Message);
        }
    }

    public void Dispose() => _fileState.ChangesChanged -= OnChangesChanged;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task AcceptAsync()
    {
        try
        {
            foreach (var file in Files)
            {
                await SaveIfOpenAsync(file.Change.Path);
            }
        }
        catch (IOException exception)
        {
            _statusBar.Message = Format(Strings.ChangesNotAccepted, exception.Message);
            return;
        }

        // Accepting updates the list via ChangesChanged, so take the count first.
        var count = Files.Count;
        _fileState.AcceptChanges();
        _statusBar.Message = Format(Strings.ChangesAccepted, FileCount(count));
        Files = [];
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task RevertAllAsync()
    {
        var count = Files.Count;
        if (await TryRevertAsync(Files))
        {
            _statusBar.Message = Format(Strings.ChangesReverted, FileCount(count));
        }

        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanChangeFile))]
    private async Task RevertFileAsync(ChangedFileViewModel? file)
    {
        if (await TryRevertAsync([file!]))
        {
            _statusBar.Message = Format(Strings.FileReverted, file!.Change.RelativePath);
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task OpenFileAsync(ChangedFileViewModel? file)
    {
        if (file is { Change.Current: not null })
        {
            await _commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(file.Change.Path));
        }
    }

    private bool CanChange() => !IsAgentBusy;

    private bool CanChangeFile(ChangedFileViewModel? file) => file is not null && !IsAgentBusy;

    // While the agent works the list refreshes at the end of the turn; editor decisions refresh it at once.
    private void OnChangesChanged(object? sender, string? path)
    {
        if (!IsAgentBusy)
        {
            _dispatcher.Post(() => _ = RefreshAsync());
        }
    }

    // The agent must re-read a reverted file before editing it: its version is no longer the one it saw.
    private async Task<bool> TryRevertAsync(IReadOnlyList<ChangedFileViewModel> files)
    {
        foreach (var file in files)
        {
            try
            {
                await _reverter.RevertAsync(file.Change.Path, file.Change.Original);
                _fileState.Forget(file.Change.Path);
                _fileState.RecordRejected(file.Change.Path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                _statusBar.Message = Format(Strings.RevertFailed, file.Change.RelativePath, exception.Message);
                return false;
            }
        }

        return true;
    }

    private async Task SaveIfOpenAsync(string path)
    {
        Task? saving = null;
        await _dispatcher.InvokeAsync(() =>
        {
            if (_documents.TryGet(path, out var document) && document is { IsDirty: true, IsDeletedOnDisk: false })
            {
                saving = _documents.SaveAsync(document);
            }
        });

        if (saving is not null)
        {
            await saving;
        }
    }

    private static string FileCount(int count) => Plural.Format(count, Strings.FileForms);

    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
