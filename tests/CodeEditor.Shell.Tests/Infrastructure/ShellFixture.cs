using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Storage;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Workspace;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Infrastructure;

/// <summary>Real core services, an in-memory file system and a temporary user data folder.</summary>
internal sealed class ShellFixture : IDisposable
{
    public ShellFixture()
    {
        CommandService = new CommandService(Commands, Context, NullLogger<CommandService>.Instance);
        MenuBuilder = new MenuBuilder(Menus, Commands, Keybindings, CommandService, Context);
        MenuFactory = new MenuViewModelFactory(MenuBuilder, Menus, Commands, Keybindings);
        Workspace = new Core.Files.Workspace(FileSystem, Context, NullLogger<Core.Files.Workspace>.Instance);
        Paths = new UserDataPaths(Path.Combine(Path.GetTempPath(), "CodeEditor.Tests", Guid.NewGuid().ToString("N")));
        RecentFolders = new RecentFolders(Paths, NullLogger<RecentFolders>.Instance);
    }

    public ContextKeyService Context { get; } = new();

    public CommandRegistry Commands { get; } = new();

    public KeybindingRegistry Keybindings { get; } = new();

    public MenuRegistry Menus { get; } = new();

    public CommandService CommandService { get; }

    public MenuBuilder MenuBuilder { get; }

    public MenuViewModelFactory MenuFactory { get; }

    public FakeFileSystem FileSystem { get; } = new();

    public Core.Files.Workspace Workspace { get; }

    public UserDataPaths Paths { get; }

    public RecentFolders RecentFolders { get; }

    public IDisposable RegisterCommand(string id, string title = "Команда", string? category = null, string? when = null) =>
        Commands.Register(new CommandDefinition(
            id,
            title,
            (_, _) => ValueTask.CompletedTask,
            category,
            when is null ? null : ContextExpression.Parse(when)));

    public IDisposable Bind(string keys, string commandId) =>
        Keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId));

    public void Dispose()
    {
        Workspace.Dispose();
        if (Directory.Exists(Paths.Root))
        {
            Directory.Delete(Paths.Root, recursive: true);
        }
    }
}
