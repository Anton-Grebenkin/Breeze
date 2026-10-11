using System.Globalization;
using System.Text;
using CodeEditor.Modules.Terminal.Resources;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Background command text for the model and the feed: status on the first line (parsed by the feed row), then how
/// to read or stop it while running, then the new output.
/// </summary>
public static class BackgroundCommandText
{
    public static string Status(BackgroundCommand command, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(command);
        var seconds = Durations.Seconds(elapsed);
        return command switch
        {
            { IsRunning: true } => Format(Strings.BackgroundRunning, command.Id, seconds),
            { IsStopped: true } => Format(Strings.BackgroundStopped, command.Id, seconds),
            _ => Format(Strings.BackgroundExited, command.Id, command.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "?", seconds),
        };
    }

    /// <param name="movedByUser">The user moved the call to the background from the feed: the model shouldn't wait.</param>
    public static string Report(BackgroundCommand command, TimeSpan elapsed, string newOutput, bool movedByUser = false)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(newOutput);
        var text = new StringBuilder(Status(command, elapsed));
        if (movedByUser)
        {
            text.Append('\n').Append(Strings.MovedToBackgroundByUser);
        }

        if (command.IsRunning)
        {
            text.Append('\n').Append(Format(command.IsJob ? Strings.BackgroundJobHowTo : Strings.BackgroundHowTo, command.Id));
        }

        // A running job keeps no output of its own, only the last line: its report comes at the end.
        if (command is { IsJob: true, IsRunning: true })
        {
            return command.LastLine is { Length: > 0 } line ? text.Append('\n').Append(Format(Strings.BackgroundLastLine, line.Trim())).ToString() : text.ToString();
        }

        if (command.Dropped > 0)
        {
            text.Append('\n').Append(Format(Strings.BackgroundDropped, command.Dropped));
        }

        text.Append('\n').Append(newOutput.Length == 0 ? Strings.BackgroundNoNewOutput : newOutput.TrimEnd());
        return text.ToString();
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
