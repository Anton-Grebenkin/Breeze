using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Documents.Services;

/// <summary>
/// A tool function whose schema has no "any value" nodes: a schema node without a type (<c>{}</c>, which describes a
/// cell's JSON value) gets type "string". Not every model service accepts an empty schema, but all accept a string; a
/// number or boolean sent instead of a string is still accepted by the function.
/// </summary>
internal sealed class TextValuesFunction(AIFunction innerFunction) : DelegatingAIFunction(innerFunction)
{
    private static readonly string[] SchemaKeywords = ["type", "$ref", "anyOf", "oneOf", "allOf", "enum", "const", "properties", "items"];

    public override JsonElement JsonSchema { get; } = Typed(innerFunction.JsonSchema);

    private static JsonElement Typed(JsonElement schema)
    {
        var root = JsonNode.Parse(schema.GetRawText())!;
        VisitProperties(root);
        using var document = JsonDocument.Parse(root.ToJsonString());
        return document.RootElement.Clone();
    }

    // Walks a parameter schema: object properties and array items.
    private static void Visit(JsonNode? node)
    {
        if (node is not JsonObject schema)
        {
            return;
        }

        if (!SchemaKeywords.Any(schema.ContainsKey))
        {
            schema["type"] = "string";
            return;
        }

        Visit(schema["items"]);
        VisitProperties(schema);
    }

    private static void VisitProperties(JsonNode node)
    {
        if (node["properties"] is not JsonObject properties)
        {
            return;
        }

        foreach (var property in properties)
        {
            Visit(property.Value);
        }
    }
}
