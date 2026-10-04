using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.ViewModels;

/// <summary>
/// The title bar: menu bar on the left and a search box in the center that opens the palette. With a folder open,
/// the search box and window title show its name, as in VS Code.
/// </summary>
public sealed partial class TitleBarViewModel : ObservableObject, IDisposable
{
    private readonly ICommandService _commands;
    private readonly IKeybindingRegistry _keybindings;
    private readonly IWorkspace _workspace;

    public TitleBarViewModel(
        ICommandService commands,
        IKeybindingRegistry keybindings,
        IWorkspace workspace,
        MenuViewModelFactory menus)
    {
        _commands = commands;
        _keybindings = keybindings;
        _workspace = workspace;
        MenuBar = menus.Create(MenuIds.MenuBar);

        _keybindings.Changed += OnKeybindingsChanged;
        _workspace.Changed += OnWorkspaceChanged;
        UpdateShortcut();
        UpdateTitles();
    }

    public MenuViewModel MenuBar { get; }

    /// <summary>Search box text: the folder name or "Search". Clicking opens quick open (<c>Ctrl+P</c>).</summary>
    [ObservableProperty]
    public partial string SearchText { get; private set; } = Strings.TitleBarSearch;

    /// <summary>Window title for the taskbar: "folder — Breeze".</summary>
    [ObservableProperty]
    public partial string WindowTitle { get; private set; } = MainWindowViewModel.ProductName;

    /// <summary>Quick open keybinding, or <c>null</c> if unbound.</summary>
    [ObservableProperty]
    public partial string? SearchShortcut { get; private set; }

    public void Dispose()
    {
        _keybindings.Changed -= OnKeybindingsChanged;
        _workspace.Changed -= OnWorkspaceChanged;
        MenuBar.Dispose();
    }

    [RelayCommand]
    private async Task QuickOpenAsync() => await _commands.ExecuteAsync(ShellCommandIds.QuickOpen);

    private void OnKeybindingsChanged(object? sender, EventArgs e) => UpdateShortcut();

    private void OnWorkspaceChanged(object? sender, EventArgs e) => UpdateTitles();

    private void UpdateShortcut() =>
        SearchShortcut = _keybindings.FindForCommand(ShellCommandIds.QuickOpen)?.Sequence.ToString();

    private void UpdateTitles()
    {
        var name = _workspace.Name;
        SearchText = name ?? Strings.TitleBarSearch;
        WindowTitle = name is null ? MainWindowViewModel.ProductName : $"{name} — {MainWindowViewModel.ProductName}";
    }
}
