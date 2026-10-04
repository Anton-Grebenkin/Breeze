using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Contracts;

/// <summary>
/// Marks tools whose result comes from outside: a web page, search, the browser. Such text may contain instructions for
/// the model, so the agent extracts no memory from a turn that called them (ADR 0029): foreign text must not become a
/// note that steers later chats. The mark is a function property, preserved by wrappers
/// (<see cref="DelegatingAIFunction"/>).
/// </summary>
public static class ExternalContent
{
    public const string PropertyName = "codeeditor.externalContent";

    private static readonly IReadOnlyDictionary<string, object?> Marker = new Dictionary<string, object?>(StringComparer.Ordinal) { [PropertyName] = true };

    /// <summary>A marked tool function, like <c>AIFunctionFactory.Create(method, name, description)</c>.</summary>
    public static AIFunction Create(Delegate method, string name, string description) =>
        AIFunctionFactory.Create(method, new AIFunctionFactoryOptions { Name = name, Description = description, AdditionalProperties = Marker });

    public static bool Has(AITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool.AdditionalProperties.TryGetValue(PropertyName, out var value) && value is true;
    }
}
