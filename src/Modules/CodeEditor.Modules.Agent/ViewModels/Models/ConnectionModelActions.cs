namespace CodeEditor.Modules.Agent.ViewModels.Models;

/// <summary>What a model row asks of its connection.</summary>
/// <param name="Changed">Saves the connection with the row as it is now.</param>
/// <param name="Use">Makes the model the conversation model.</param>
/// <param name="Check">Runs the model check.</param>
/// <param name="Apply">Takes what a check found about the whole connection.</param>
public sealed record ConnectionModelActions(
    Action<ConnectionModelRowViewModel> Changed,
    Action<ConnectionModelRowViewModel> Use,
    Func<ConnectionModelRowViewModel, CancellationToken, Task<ModelProbeReport>> Check,
    Action<ModelProbeReport> Apply);
