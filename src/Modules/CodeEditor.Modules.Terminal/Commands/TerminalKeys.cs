using System.Collections.Frozen;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.ToolWindows;
using CodeEditor.Shell.Workspace;

namespace CodeEditor.Modules.Terminal.Commands;

/// <summary>
/// Commands that keep their keys while a terminal has focus, as VS Code's "commands to skip shell": the palette, quick
/// open, panels and layout, zoom, new window and the terminal's own. Every other key goes to the shell: <c>Ctrl+K</c>,
/// <c>Ctrl+W</c>, <c>Ctrl+R</c>, <c>Ctrl+L</c>.
/// </summary>
public static class TerminalKeys
{
    private static readonly string ShowViewPrefix = ToolWindowAreaViewModel.ShowCommandId(string.Empty);

    private static readonly FrozenSet<string> Commands = new[]
    {
        ShellCommandIds.ShowCommands,
        ShellCommandIds.QuickOpen,
        ShellCommandIds.NewTerminal,
        TerminalPanelCommands.KillId,
        WorkspaceCommands.NewWindowId,
        LayoutCommands.ToggleSideBarId,
        LayoutCommands.ToggleSecondarySideBarId,
        LayoutCommands.TogglePanelId,
        LayoutCommands.ToggleMaximizedPanelId,
        ZoomCommands.ZoomInId,
        ZoomCommands.ZoomOutId,
        ZoomCommands.ZoomResetId,
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool KeepsKeys(string commandId) =>
        Commands.Contains(commandId) || commandId.StartsWith(ShowViewPrefix, StringComparison.Ordinal);
}
