namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>A running tool call for its feed row (<see cref="RunningToolCalls.Snapshot"/>).</summary>
/// <param name="LastLine">The latest non-blank output line; <c>null</c> for a tool without output.</param>
/// <param name="OffersBackground">The tool waits for a process and can let it go on in the background.</param>
public readonly record struct ToolCallState(string CallId, TimeSpan Elapsed, string? LastLine, bool OffersBackground);
