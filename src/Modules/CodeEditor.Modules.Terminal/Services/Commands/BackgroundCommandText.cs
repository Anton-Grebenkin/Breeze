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

    public static string Report(BackgroundCommand command, TimeSpan elapsed, string newOutput)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(newOutput);
        var text = new StringBuilder(Status(command, elapsed));
        if (command.IsRunning)
        {
            text.Append('\n').Append(Format(Strings.BackgroundHowTo, command.Id));
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
