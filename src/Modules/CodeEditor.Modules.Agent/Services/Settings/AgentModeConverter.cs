using System.ComponentModel;
using System.Globalization;

namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>
/// Reads <c>agent.mode</c> from settings: the legacy "light / medium / power" modes (ADR 0008) map to Agent mode,
/// otherwise an old <c>settings.json</c> would break settings loading.
/// </summary>
public sealed class AgentModeConverter() : EnumConverter(typeof(AgentMode))
{
    private static readonly string[] LegacyModes = ["light", "medium", "power"];

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text && LegacyModes.Contains(text.Trim(), StringComparer.OrdinalIgnoreCase)
            ? AgentMode.Agent
            : base.ConvertFrom(context, culture, value);
}
