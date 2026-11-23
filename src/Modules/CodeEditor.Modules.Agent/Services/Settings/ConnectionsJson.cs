using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>
/// Connections as the value of <c>agent.connections</c> in <c>settings.json</c>: only what is set, in the order a
/// person would write it, so the file stays readable and editable by hand.
/// </summary>
internal static class ConnectionsJson
{
    public static JsonArray ToNode(IEnumerable<AgentConnection> connections) => new([.. connections.Select(Connection)]);

    private static JsonNode Connection(AgentConnection connection)
    {
        var node = new JsonObject { ["id"] = connection.Id };
        Add(node, "name", connection.Name);
        node["endpoint"] = connection.Endpoint;
        if (connection.Api != AgentApi.Auto)
        {
            node["api"] = Name(connection.Api);
        }

        if (connection.Headers.Count > 0)
        {
            node["headers"] = new JsonObject(connection.Headers.Select(static header => KeyValuePair.Create(header.Key, (JsonNode?)header.Value)));
        }

        Add(node, "keyless", connection.Keyless ? true : null);
        Add(node, "legacyMaxTokens", connection.LegacyMaxTokens ? true : null);
        if (connection.Models.Count > 0)
        {
            node["models"] = new JsonArray([.. connection.Models.Select(Model)]);
        }

        return node;
    }

    private static JsonNode Model(ModelDescription model)
    {
        var node = new JsonObject { ["id"] = model.Id };
        Add(node, "name", model.Name);
        Add(node, "family", model.Family is { } family ? Name(family) : null);
        Add(node, "contextWindow", model.ContextWindow);
        Add(node, "maxOutputTokens", model.MaxOutputTokens);
        Add(node, "images", model.Images);
        Add(node, "reasoning", model.Reasoning is { } reasoning ? Name(reasoning) : null);
        Add(node, "editFormat", model.EditFormat is { } format ? Name(format) : null);
        return node;
    }

    private static void Add(JsonObject node, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            node[name] = value;
        }
    }

    private static void Add(JsonObject node, string name, int? value)
    {
        if (value is { } number)
        {
            node[name] = number;
        }
    }

    private static void Add(JsonObject node, string name, bool? value)
    {
        if (value is { } flag)
        {
            node[name] = flag;
        }
    }

    // "deepSeek", "chatCompletions": the settings reader takes enum names in any case.
    private static string Name<T>(T value)
        where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
