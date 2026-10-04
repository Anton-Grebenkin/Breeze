using CodeEditor.Modules.Agent.Contracts.Verification;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>Test verification gate: records results and returns the configured hint.</summary>
internal sealed class RecordingVerificationLog : IAgentVerificationLog
{
    public List<(VerificationKind Kind, bool Succeeded)> Records { get; } = [];

    public string? Hint { get; set; }

    public string? Record(VerificationKind kind, bool succeeded)
    {
        Records.Add((kind, succeeded));
        return Hint;
    }
}
