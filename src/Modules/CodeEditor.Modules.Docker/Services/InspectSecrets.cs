using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodeEditor.Core.Processes;

namespace CodeEditor.Modules.Docker.Services;

/// <summary>
/// Hides secrets in <c>docker inspect</c> output: container and image environment values whose names suggest a key,
/// token or password (<see cref="SecretVariables"/>) become "***", since the output goes to an external model. The
/// variable name stays, so the model sees that it is set.
/// </summary>
public static class InspectSecrets
{
    public const string Mask = "***";

    private const string EnvironmentProperty = "Env";

    // No escaping of '&', '<' or non-ASCII text, so commands and paths stay readable.
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>JSON with hidden values; returned as is when there are no secrets or it is not JSON.</summary>
    public static string Hide(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            return JsonNode.Parse(json) is { } root && HideIn(root) ? root.ToJsonString(Indented) : json;
        }
        catch (JsonException)
        {
            return json;
        }
    }

    // Tree walk, O(nodes); environment variables are an array of "NAME=value" strings in the Env property.
    private static bool HideIn(JsonNode? node)
    {
        var hidden = false;
        if (node is JsonObject properties)
        {
            foreach (var (name, value) in properties)
            {
                hidden |= name == EnvironmentProperty && value is JsonArray variables ? HideVariables(variables) : HideIn(value);
            }
        }
        else if (node is JsonArray items)
        {
            foreach (var item in items)
            {
                hidden |= HideIn(item);
            }
        }

        return hidden;
    }

    private static bool HideVariables(JsonArray variables)
    {
        var hidden = false;
        for (var index = 0; index < variables.Count; index++)
        {
            if (variables[index] is JsonValue value && value.TryGetValue<string>(out var variable) && Secret(variable) is { } name)
            {
                variables[index] = name + "=" + Mask;
                hidden = true;
            }
        }

        return hidden;
    }

    private static string? Secret(string variable) =>
        variable.IndexOf('=', StringComparison.Ordinal) is > 0 and var separator && separator < variable.Length - 1
            && SecretVariables.IsSecret(variable[..separator])
            ? variable[..separator]
            : null;
}
