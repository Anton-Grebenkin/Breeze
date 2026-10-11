using System.Collections.Concurrent;
using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Turn;
using CodeEditor.Modules.Terminal.Resources;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Tells the model that a background command finished, in the next tool result (<see cref="IAgentTurnNotes"/>):
/// otherwise it learned only from its next <c>command_output</c> call or the user's next message, and kept waiting or
/// forgot the run. A command whose final status the model already read is not announced.
/// </summary>
public sealed class BackgroundCommandNotes : IAgentTurnNotes, IDisposable
{
    private const int MaxCommandLength = 80;

    private readonly BackgroundCommands _commands;
    private readonly ConcurrentQueue<BackgroundCommand> _finished = new();

    public BackgroundCommandNotes(BackgroundCommands commands)
    {
        _commands = commands;
        _commands.DetachedFinished += OnDetachedFinished;
    }

    public IReadOnlyList<string> TakeNotes()
    {
        var notes = new List<string>();
        while (_finished.TryDequeue(out var command))
        {
            if (command.IsReported)
            {
                continue;
            }

            command.IsReported = true;
            var status = BackgroundCommandText.Status(command, _commands.Elapsed(command));
            notes.Add(string.Format(CultureInfo.CurrentCulture, Strings.BackgroundFinishedNote, status, Shorten(command.Command), command.Id));
        }

        return notes;
    }

    public void Dispose() => _commands.DetachedFinished -= OnDetachedFinished;

    private void OnDetachedFinished(object? sender, BackgroundCommand command) => _finished.Enqueue(command);

    private static string Shorten(string command)
    {
        var line = command.ReplaceLineEndings(" ");
        return line.Length <= MaxCommandLength ? line : string.Concat(line.AsSpan(0, MaxCommandLength), "…");
    }
}
