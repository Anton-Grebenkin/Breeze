using CodeEditor.Modules.Agent.Contracts.Verification;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// What the turn lacks: a <paramref name="Kind"/> check after the last edit; <paramref name="Failed"/> if it ran but failed.
/// </summary>
public readonly record struct VerificationGap(VerificationKind Kind, bool Failed);
