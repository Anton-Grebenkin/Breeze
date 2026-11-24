namespace CodeEditor.Modules.Agent.Services.Probes;

/// <summary>One line of the model check report.</summary>
/// <param name="Passed"><c>null</c> when the answer is neither good nor bad, only a fact about the model.</param>
/// <param name="Detail">What was found, in the user's language.</param>
public sealed record ModelProbeStep(ModelProbeKind Kind, bool? Passed, string Detail);
