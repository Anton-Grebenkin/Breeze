using System.Text.Json;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>
/// Mode catalog: title, picker description and whether the mode changes code. Available tools are decided by
/// <see cref="ToolPolicy"/>. One model request limit per turn for all modes, as in Copilot; then "Continue".
/// </summary>
public static class AgentModes
{
    /// <summary>Model requests per turn; the last one is summary only, without tools.</summary>
    public const int RequestLimit = 50;

    public static IReadOnlyList<AgentMode> All { get; } = [AgentMode.Agent, AgentMode.Deep, AgentMode.Ask, AgentMode.Plan];

    /// <summary>
    /// Modes in the input picker. Deep is hidden pending review: it still works when enabled via
    /// <c>agent.mode: deep</c> in settings or by the benchmark, but the UI does not offer it.
    /// </summary>
    public static IReadOnlyList<AgentMode> Selectable { get; } = [AgentMode.Agent, AgentMode.Ask, AgentMode.Plan];

    /// <summary>The mode changes code: edits, commands, build and tests are allowed.</summary>
    public static bool CanChange(AgentMode mode) => mode is AgentMode.Agent or AgentMode.Deep;

    public static string Title(AgentMode mode) => mode switch
    {
        AgentMode.Deep => Strings.ModeDeep,
        AgentMode.Ask => Strings.ModeAsk,
        AgentMode.Plan => Strings.ModePlan,
        _ => Strings.ModeAgent,
    };

    public static string Description(AgentMode mode) => mode switch
    {
        AgentMode.Deep => Strings.ModeDeepDescription,
        AgentMode.Ask => Strings.ModeAskDescription,
        AgentMode.Plan => Strings.ModePlanDescription,
        _ => Strings.ModeAgentDescription,
    };

    /// <summary>The <c>settings.json</c> value: <c>agent</c>, <c>deep</c>, <c>ask</c>, <c>plan</c>.</summary>
    public static string SettingValue(AgentMode mode) => JsonNamingPolicy.CamelCase.ConvertName(mode.ToString());
}
