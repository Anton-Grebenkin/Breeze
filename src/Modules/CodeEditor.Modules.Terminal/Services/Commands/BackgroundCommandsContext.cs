using CodeEditor.Modules.Agent.Contracts.Context;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// The last <see cref="MaxShown"/> background commands in every message's <c>&lt;context&gt;</c> (ADR 0012), so the
/// model knows a server is still running or a long run finished without an extra call.
/// </summary>
public sealed class BackgroundCommandsContext(BackgroundCommands commands) : IAgentContextProvider
{
    public const int MaxShown = 5;

    public ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> lines =
        [
            .. commands.All.TakeLast(MaxShown).Select(command => $"{BackgroundCommandText.Status(command, commands.Elapsed(command))} `{command.Command}`"),
        ];
        return ValueTask.FromResult(lines);
    }
}
