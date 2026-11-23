using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Agent commands that antivirus blocks even without the launcher's wrapper (ADR 0052). The model gets the reason in
/// plain words instead of a PowerShell error to guess from; the second block in a row switches <c>run_command</c> off
/// for the turn, so it is not tried ten more times; the user is told once per session, because the model's own
/// explanation may be wrong.
/// </summary>
public sealed class CommandBlocks(INotificationService notifications, IUiDispatcher dispatcher)
{
    public const string NotificationId = "agentCommandsBlocked";

    /// <summary>Blocks in a row, of any commands, after which the tool is switched off for the turn.</summary>
    public const int SwitchOffAfter = 2;

    private int _inRow;
    private int _notified;

    /// <summary>A command ran: whatever was blocked before is not a property of this machine.</summary>
    public void Ran() => Interlocked.Exchange(ref _inRow, 0);

    /// <param name="output">What PowerShell printed: the antivirus message.</param>
    /// <returns>The error for the model.</returns>
    public AgentToolException Blocked(string output)
    {
        var inRow = Interlocked.Increment(ref _inRow);
        if (Interlocked.Exchange(ref _notified, 1) == 0)
        {
            dispatcher.Post(() => _ = notifications.Show(new Notification(NotificationId, Strings.CommandsBlockedNotification, [])));
        }

        return new AgentToolException(Strings.CommandBlockedByAntivirus + "\n" + output) { ToolUnavailable = inRow >= SwitchOffAfter };
    }
}
