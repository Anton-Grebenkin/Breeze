using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Tools.Resources;
using CodeEditor.Modules.Tools.Services;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Modules.Tools.Commands;

/// <summary>
/// The "Tools" menu and palette commands: one command per tool on the shelf (re-registered when the shelf changes),
/// "Run Tool…", "New Tool…" and "Refresh". A tool with new or changed code asks before it runs.
/// </summary>
public sealed class ToolCommands(
    ToolShelf shelf,
    ToolRunner runner,
    ToolTrust trust,
    ToolActivity activity,
    ToolParameterPrompt prompt,
    ToolScaffold scaffold,
    IDialogService dialogs,
    IQuickPick quickPick,
    ICommandService commandService,
    IUiDispatcher dispatcher,
    StatusBarViewModel statusBar) : IDisposable
{
    public const string MenuId = "menubar.tools";

    private const int MenuBarOrder = 4;
    private const string ToolsGroup = "1_tools";
    private const string ManageGroup = "2_manage";

    private readonly List<IDisposable> _registrations = [];
    private readonly List<IDisposable> _toolRegistrations = [];
    private ICommandRegistry? _commands;
    private IMenuRegistry? _menus;

    private static string Category => Strings.Category;

    public void Register(ICommandRegistry commands, IMenuRegistry menus)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(menus);
        _commands = commands;
        _menus = menus;
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.MenuBar, MenuId, Strings.Menu, order: MenuBarOrder)));
        Add(ToolCommandIds.RunTool, Strings.CommandRunTool, PickTool, order: 1);
        Add(ToolCommandIds.NewTool, Strings.CommandNewTool, AskNewTool, order: 2);
        Add(ToolCommandIds.Refresh, Strings.CommandRefresh, shelf.Refresh, order: 3);
        shelf.Changed += OnShelfChanged;
        RegisterTools();
    }

    public void Dispose()
    {
        shelf.Changed -= OnShelfChanged;
        _toolRegistrations.ForEach(registration => registration.Dispose());
        _toolRegistrations.Clear();
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    /// <summary>Runs a tool by name; parameters come from <paramref name="argument"/> or the palette.</summary>
    public async Task RunAsync(string name, object? argument)
    {
        if (shelf.Find(name) is not { } tool)
        {
            statusBar.Message = Format(Strings.ToolMissing, name);
            return;
        }

        if (argument is IReadOnlyDictionary<string, string> values)
        {
            await RunToolAsync(tool, values);
        }
        else if (tool.Parameters.Count == 0)
        {
            await RunToolAsync(tool, new Dictionary<string, string>());
        }
        else
        {
            await prompt.AskAsync(tool, given => RunToolAsync(tool, given));
        }
    }

    private async Task RunToolAsync(ToolDefinition tool, IReadOnlyDictionary<string, string> values)
    {
        if (!trust.IsTrusted(tool))
        {
            var hash = trust.Hash(tool);
            if (!dialogs.Confirm(Format(Strings.ConfirmRun, tool.Name), Format(Strings.ConfirmRunDetail, tool.Script), Strings.Run))
            {
                return;
            }

            trust.Trust(hash);
        }

        try
        {
            var request = runner.Request(tool, values, forAgent: false);
            activity.Started(tool.Name, runner.Display(request));
            activity.Finished(tool.Name, await runner.RunAsync(request, activity.Line, CancellationToken.None));
        }
        catch (Exception exception) when (exception is AgentToolException or InvalidOperationException)
        {
            activity.Failed(tool.Name, exception.Message);
        }
    }

    private void RegisterTools()
    {
        _toolRegistrations.ForEach(registration => registration.Dispose());
        _toolRegistrations.Clear();
        if (_commands is null || _menus is null)
        {
            return;
        }

        var order = 0;
        foreach (var tool in shelf.Tools)
        {
            var id = ToolCommandIds.Run(tool.Name);
            var name = tool.Name;
            _toolRegistrations.Add(_commands.Register(new CommandDefinition(id, name, async (argument, _) => await RunAsync(name, argument), Category)));
            _toolRegistrations.Add(_menus.Register(MenuItemDefinition.ForCommand(MenuId, id, ToolsGroup, order++)));
        }
    }

    private void OnShelfChanged(object? sender, EventArgs e) => dispatcher.Post(RegisterTools);

    private void PickTool()
    {
        var items = shelf.Tools.Select(tool => new QuickPickItem(tool.Name, tool.Name) { Detail = tool.Description }).ToList();
        quickPick.Show(new QuickPickProvider(Strings.PickToolPlaceholder, items, item => RunAsync(item.Id, null)) { EmptyText = Strings.NoTools });
    }

    private void AskNewTool() =>
        quickPick.Show(new QuickPickProvider(Strings.NewToolPlaceholder, [], item => CreateToolAsync(item.Id))
        {
            CustomItem = text => ToolDefinitionReader.IsValidName(text) ? new QuickPickItem(text, Format(Strings.NewToolNamed, text)) : null,
            EmptyText = Strings.NewToolNameRules,
        });

    private async Task CreateToolAsync(string name)
    {
        string? path;
        try
        {
            path = scaffold.Create(name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            statusBar.Message = exception.Message;
            return;
        }

        if (path is null)
        {
            statusBar.Message = Strings.NoFolder;
            return;
        }

        shelf.Refresh();
        await commandService.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(path));
    }

    private void Add(string id, string title, Action handler, int order)
    {
        _registrations.Add(_commands!.Register(new CommandDefinition(id, title, (_, _) =>
        {
            handler();
            return ValueTask.CompletedTask;
        }, Category)));
        _registrations.Add(_menus!.Register(MenuItemDefinition.ForCommand(MenuId, id, ManageGroup, order)));
    }

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
