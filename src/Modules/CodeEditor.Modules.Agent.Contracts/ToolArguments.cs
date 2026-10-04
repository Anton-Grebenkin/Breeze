using System.Globalization;
using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts.Resources;
using CodeEditor.Modules.Agent.Contracts.Text;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Contracts;

/// <summary>
/// Tool call arguments in the requested type: they arrive as JSON from the model and as objects in tests.
/// Serialized with the same options as the tool schema (<see cref="AIJsonUtilities.DefaultOptions"/>).
/// </summary>
public static class ToolArguments
{
    /// <returns>The value, or <c>default</c> if the argument is missing.</returns>
    public static T? GetOptional<T>(IDictionary<string, object?> arguments, string name)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.TryGetValue(name, out var value) && value is not null && value is not JsonElement { ValueKind: JsonValueKind.Null }
            ? Get<T>(arguments, name)
            : default;
    }

    public static T Get<T>(IDictionary<string, object?> arguments, string name)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.ArgumentMissing, name));
        }

        if (value is T typed)
        {
            return typed;
        }

        try
        {
            var element = value as JsonElement? ?? JsonSerializer.SerializeToElement(value, AIJsonUtilities.DefaultOptions);
            // Some models send an array or object as a JSON string; parse it as the value.
            if (typeof(T) != typeof(string) && element.ValueKind == JsonValueKind.String && element.GetString() is { } text && text.AsSpan().TrimStart() is ['[' or '{', ..])
            {
                using var document = JsonDocument.Parse(JsonBracketRepair.Balance(text));
                element = document.RootElement.Clone();
            }

            return element.Deserialize<T>(AIJsonUtilities.DefaultOptions)
                ?? throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.ArgumentEmpty, name));
        }
        catch (JsonException exception)
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.ArgumentInvalid, name, exception.Message));
        }
    }
}
