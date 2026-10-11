using CodeEditor.Modules.Agent.Contracts.Turn;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Builds and test runs that went to the background and still run (<see cref="IAgentRunningJobs"/>). Servers started
/// with <c>run_command</c> are not jobs: they are meant to keep running.
/// </summary>
public sealed class RunningJobs(BackgroundCommands commands) : IAgentRunningJobs
{
    public IReadOnlyList<string> Running() =>
        [.. commands.All.Where(static command => command is { IsJob: true, IsRunning: true, IsDetached: true }).Select(static command => $"#{command.Id} `{command.Command}`")];
}
