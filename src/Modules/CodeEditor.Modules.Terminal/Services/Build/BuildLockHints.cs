using System.Collections.Frozen;
using System.Globalization;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.Services.Commands;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// Advice for a build that could not overwrite its outputs because a running process holds them
/// (<see cref="LockedFiles"/>). Usually it is the app the agent started earlier with <c>dotnet run</c> and forgot:
/// a rebuild fails the same way until it stops, so the hint names the background command to stop.
/// </summary>
public sealed class BuildLockHints(BackgroundCommands background)
{
    // Hosts whose name appears in almost every command, so it doesn't point to the one that started them.
    private static readonly FrozenSet<string> Hosts = FrozenSet.ToFrozenSet(["dotnet", "testhost"], StringComparer.OrdinalIgnoreCase);

    /// <returns>The hint after a blank line; empty if no file was locked.</returns>
    public string For(IEnumerable<BuildDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var processes = LockedFiles.Of(string.Join('\n', diagnostics.Select(static diagnostic => diagnostic.Message)));
        if (processes.Count == 0)
        {
            return string.Empty;
        }

        var names = string.Join(", ", processes.Select(static process => string.Create(CultureInfo.InvariantCulture, $"{process.Name} ({process.Id})")));
        var running = background.All.Where(static command => command is { IsRunning: true, IsJob: false }).ToList();
        if (running.Find(command => processes.Any(process => Started(command, process))) is { } owner)
        {
            return "\n\n" + Format(Strings.BuildLockedByBackground, names, owner.Id, owner.Command);
        }

        var hint = "\n\n" + Format(Strings.BuildLocked, names);
        return running.Count == 0
            ? hint
            : hint + " " + Format(Strings.RunningBackgroundCommands, string.Join(", ", running.Select(static command => $"#{command.Id} `{command.Command}`")));
    }

    private static bool Started(BackgroundCommand command, LockingProcess process) =>
        !Hosts.Contains(process.Name) && command.Command.Contains(process.Name, StringComparison.OrdinalIgnoreCase);

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
