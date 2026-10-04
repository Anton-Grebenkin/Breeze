using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.ViewModels.Tabs;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Docker.Commands;

/// <summary>
/// Palette commands for the log and inspect tabs, mirroring their toolbar buttons. They act on the active tab and do
/// nothing for other tabs, like the agent's edit commands in the editor.
/// </summary>
public sealed class DockerTabCommands(EditorAreaViewModel editors) : IDisposable
{
    public const string LogsReconnectId = "docker.logs.reconnect";
    public const string LogsClearId = "docker.logs.clear";
    public const string LogsAutoScrollId = "docker.logs.toggleAutoScroll";
    public const string LogsWordWrapId = "docker.logs.toggleWordWrap";
    public const string InspectRefreshId = "docker.inspect.refresh";
    public const string InspectCopyId = "docker.inspect.copy";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var editorOpen = ContextExpression.Parse(EditorAreaViewModel.EditorOpenContextKey);
        AddForLog(commands, LogsReconnectId, Strings.CommandLogsReconnect, log => log.Start(), editorOpen);
        AddForLog(commands, LogsClearId, Strings.CommandLogsClear, log => log.Clear(), editorOpen);
        AddForLog(commands, LogsAutoScrollId, Strings.CommandLogsAutoScroll, log => log.AutoScroll = !log.AutoScroll, editorOpen);
        AddForLog(commands, LogsWordWrapId, Strings.CommandLogsWordWrap, log => log.WordWrap = !log.WordWrap, editorOpen);
        Add(commands, InspectRefreshId, Strings.CommandInspectRefresh, async () =>
        {
            if (editors.Active?.Editor is InspectViewModel inspect)
            {
                await inspect.RefreshAsync();
            }
        }, editorOpen);
        Add(commands, InspectCopyId, Strings.CommandInspectCopy, () =>
        {
            if (editors.Active?.Editor is InspectViewModel inspect && inspect.CopyCommand.CanExecute(null))
            {
                inspect.CopyCommand.Execute(null);
            }

            return Task.CompletedTask;
        }, editorOpen);
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private void AddForLog(ICommandRegistry commands, string id, string title, Action<ContainerLogViewModel> action, ContextExpression when) =>
        Add(commands, id, title, () =>
        {
            if (editors.Active?.Editor is ContainerLogViewModel log)
            {
                action(log);
            }

            return Task.CompletedTask;
        }, when);

    private void Add(ICommandRegistry commands, string id, string title, Func<Task> handler, ContextExpression when) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, async (_, _) => await handler(), Strings.ModuleName, when)));
}
