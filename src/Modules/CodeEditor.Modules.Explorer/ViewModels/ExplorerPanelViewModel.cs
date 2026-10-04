using CodeEditor.Modules.Explorer.Commands;
using CodeEditor.Shell.Menus;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Explorer.ViewModels;

/// <summary>
/// Explorer panel content: the tree, header actions, context menu, name editing and drag and drop. Created on first
/// show.
/// </summary>
public sealed partial class ExplorerPanelViewModel(
    ExplorerViewModel explorer,
    ExplorerEditor editor,
    ExplorerDrop drop,
    MenuViewModelFactory menus) : IDisposable
{
    public ExplorerViewModel Explorer { get; } = explorer;

    /// <summary>Moving and copying files dropped onto the tree.</summary>
    public ExplorerDrop Drop { get; } = drop;

    public MenuViewModel ContextMenu { get; } = menus.Create(ExplorerCommands.ContextMenuId);

    public void Dispose() => ContextMenu.Dispose();

    /// <summary>Keyboard focus within the tree; gates <c>F2</c>, <c>Del</c> and <c>Enter</c>.</summary>
    public void SetFocused(bool isFocused) => Explorer.IsFocused = isFocused;

    [RelayCommand]
    private async Task NewFileAsync() => await editor.BeginCreateAsync(isDirectory: false);

    [RelayCommand]
    private async Task NewFolderAsync() => await editor.BeginCreateAsync(isDirectory: true);

    /// <summary>Double click: opens the file in a regular tab.</summary>
    [RelayCommand]
    private async Task OpenAsync(FileNodeViewModel? node) => await Explorer.OpenAsync(node);

    /// <summary>Single click on a file: preview tab, as in VS Code.</summary>
    [RelayCommand]
    private async Task PreviewAsync(FileNodeViewModel? node)
    {
        if (node is { IsDirectory: false })
        {
            await Explorer.OpenAsync(node, preview: true);
        }
    }

    /// <summary><c>Enter</c> in the name box. An invalid name keeps the box open with a status bar hint.</summary>
    [RelayCommand]
    private async Task CommitEditAsync() => await editor.CommitAsync();

    [RelayCommand]
    private void CancelEdit() => editor.CancelEditing();

    /// <summary>The name box lost focus: commit, or cancel if the name is invalid, as in VS Code.</summary>
    [RelayCommand]
    private async Task CommitOrCancelEditAsync()
    {
        if (!await editor.CommitAsync())
        {
            editor.CancelEditing();
        }
    }
}
