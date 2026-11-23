namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>A model request waits to be repeated after a service failure.</summary>
/// <param name="Reason">What happened, in the user's words (<see cref="AgentErrors.Describe"/>).</param>
/// <param name="Attempt">Which repeat this is, from 1.</param>
/// <param name="Attempts">How many repeats there are at most.</param>
public sealed record RequestRetry(string Reason, TimeSpan Delay, int Attempt, int Attempts);
