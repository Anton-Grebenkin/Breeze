namespace CodeEditor.Modules.Agent.Services.Hooks;

/// <summary>What the hooks of an event said (<see cref="HookRunner"/>).</summary>
/// <param name="Blocked">A hook exited with code 2: block the call or the message, or go on instead of finishing.</param>
/// <param name="Message">The blocking hook's output, or the other hooks' output joined; <c>null</c> if they said nothing.</param>
public readonly record struct HookOutcome(bool Blocked, string? Message);
