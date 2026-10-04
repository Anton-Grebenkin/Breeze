using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.ViewModels;

/// <summary>
/// The welcome page shown until a document opens: key commands with keybindings and recent folders. Only registered
/// commands with a keybinding are shown; every row is clickable.
/// </summary>
public sealed partial class WelcomeViewModel : ObservableObject, IDisposable
{
    /// <summary>Recent folders to show; the rest are in File → Open Recent.</summary>
    public const int RecentLimit = 5;

    // In display order.
    private static readonly string[] FeaturedCommandIds =
    [
        WorkspaceCommands.OpenFolderId,
        ShellCommandIds.QuickOpen,
        ShellCommandIds.ShowCommands,
        ShellCommandIds.ToggleTheme,
    ];

    private readonly ICommandRegistry _commands;
    private readonly IKeybindingRegistry _keybindings;
    private readonly ICommandService _commandService;
    private readonly RecentFolders _recent;

    public WelcomeViewModel(
        ICommandRegistry commands,
        IKeybindingRegistry keybindings,
        ICommandService commandService,
        RecentFolders recent)
    {
        _commands = commands;
        _keybindings = keybindings;
        _commandService = commandService;
        _recent = recent;

        _commands.Changed += OnRegistryChanged;
        _keybindings.Changed += OnRegistryChanged;
        _recent.Changed += OnRecentChanged;
        Refresh();
        RefreshRecent();
    }

    public string Heading => MainWindowViewModel.ProductName;

    [ObservableProperty]
    public partial IReadOnlyList<ShortcutItem> Shortcuts { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecentFolders))]
    public partial IReadOnlyList<RecentFolderItem> RecentFolders { get; private set; } = [];

    public bool HasRecentFolders => RecentFolders.Count > 0;

    public void Dispose()
    {
        _commands.Changed -= OnRegistryChanged;
        _keybindings.Changed -= OnRegistryChanged;
        _recent.Changed -= OnRecentChanged;
    }

    [RelayCommand]
    private async Task ExecuteAsync(ShortcutItem? item)
    {
        if (item is not null)
        {
            await _commandService.ExecuteAsync(item.CommandId);
        }
    }

    [RelayCommand]
    private async Task OpenRecentAsync(RecentFolderItem? item)
    {
        if (item is not null)
        {
            await _commandService.ExecuteAsync(WorkspaceCommands.OpenRecentId, item.Path);
        }
    }

    private void OnRegistryChanged(object? sender, EventArgs e) => Refresh();

    private void OnRecentChanged(object? sender, EventArgs e) => RefreshRecent();

    private void Refresh()
    {
        var items = new List<ShortcutItem>(FeaturedCommandIds.Length);
        foreach (var commandId in FeaturedCommandIds)
        {
            if (_commands.TryGet(commandId, out var command) && _keybindings.FindForCommand(commandId) is { } binding)
            {
                items.Add(new ShortcutItem(commandId, command.Title, binding.Sequence.ToString()));
            }
        }

        Shortcuts = items;
    }

    private void RefreshRecent() =>
        RecentFolders =
        [
            .. _recent.Items.Take(RecentLimit).Select(path => new RecentFolderItem(
                Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path,
                path)),
        ];
}
