using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.ViewModels.Models;

/// <summary>A line of the model check report: what was asked and what was found.</summary>
public sealed class ModelProbeLine(ModelProbeStep step)
{
    public string Title { get; } = step.Kind switch
    {
        ModelProbeKind.OutputLimit => Strings.ProbeTitleOutputLimit,
        ModelProbeKind.ToolCall => Strings.ProbeTitleToolCall,
        ModelProbeKind.ParallelToolCalls => Strings.ProbeTitleParallel,
        ModelProbeKind.NestedArguments => Strings.ProbeTitleNested,
        ModelProbeKind.Image => Strings.ProbeTitleImage,
        ModelProbeKind.Reasoning => Strings.ProbeTitleReasoning,
        ModelProbeKind.Language => Strings.ProbeTitleLanguage,
        _ => Strings.ProbeTitleAnswer,
    };

    public string Detail { get; } = step.Detail;

    /// <summary>The probe failed: the line is marked.</summary>
    public bool IsFailed { get; } = step.Passed == false;

    public bool IsPassed { get; } = step.Passed == true;

    public string Kind { get; } = step.Kind.ToString();

    public override string ToString() => $"{Title}: {Detail}";
}
