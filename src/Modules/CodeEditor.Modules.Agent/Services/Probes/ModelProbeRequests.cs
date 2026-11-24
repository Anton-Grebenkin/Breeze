using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Probes;

/// <summary>
/// The Chat Completions requests of the model check. Each is as small as it can be while still asking one thing, and
/// every reply is capped: the check runs on the user's tokens.
/// </summary>
internal static class ModelProbeRequests
{
    public const string NewLimitField = "max_completion_tokens";
    public const string LegacyLimitField = "max_tokens";
    public const string ReasoningEffortField = "reasoning_effort";

    public const string WeatherTool = "get_weather";
    public const string OrderTool = "create_order";

    /// <summary>The colour of the picture sent to the model.</summary>
    public const string ImageColour = "red";

    // Room for a reasoning model to think before it answers or calls.
    private const int ReplyLimit = 512;
    private const int ReasoningLimit = 1024;

    // A 32×32 plain red PNG.
    private const string RedSquare =
        "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAAKklEQVR42mO4IyJCU8QwasGoBaMWjFowasGoBaMWjFowasGoBaMWDBULAJ3LED19/LmKAAAAAElFTkSuQmCC";

    public static JsonObject Answer(string model, string limitField) => Request(model, limitField, ReplyLimit, "Reply with the single word: OK");

    public static JsonObject ToolCall(string model, string limitField) =>
        WithTools(Request(model, limitField, ReplyLimit, "What is the weather in Paris right now? Call the get_weather tool."), Weather());

    public static JsonObject TwoToolCalls(string model, string limitField) =>
        WithTools(Request(model, limitField, ReplyLimit, "Compare the weather in Paris and in Rome right now. Call get_weather for both cities in the same reply."), Weather());

    public static JsonObject NestedArguments(string model, string limitField) =>
        WithTools(
            Request(model, limitField, ReplyLimit, "Create an order with the create_order tool: the customer is Anna Petrova who lives in Paris; 2 pieces of SKU A-1 and 1 piece of SKU B-7."),
            Order());

    public static JsonObject Image(string model, string limitField)
    {
        var request = Request(model, limitField, ReplyLimit, string.Empty);
        request["messages"] = new JsonArray(new JsonObject
        {
            ["role"] = "user",
            ["content"] = new JsonArray(
                new JsonObject { ["type"] = "text", ["text"] = "What colour is the square in the picture? Answer with one English word." },
                new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = "data:image/png;base64," + RedSquare } }),
        });
        return request;
    }

    public static JsonObject Reasoning(string model, string limitField, bool withEffort)
    {
        var request = Request(model, limitField, ReasoningLimit, "A farmer has 17 sheep, buys 23 more and then sells 9. How many sheep are left? Think it through, then answer with the number.");
        if (withEffort)
        {
            request[ReasoningEffortField] = "medium";
        }

        return request;
    }

    public static JsonObject Russian(string model, string limitField) =>
        Request(model, limitField, ReplyLimit, Strings.ProbeRussianQuestion);

    private static JsonObject Request(string model, string limitField, int limit, string question) => new()
    {
        ["model"] = model,
        ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = question }),
        [limitField] = limit,
    };

    private static JsonObject WithTools(JsonObject request, JsonObject tool)
    {
        request["tools"] = new JsonArray(tool);
        return request;
    }

    private static JsonObject Weather() => Tool(WeatherTool, "Returns the current weather in a city.", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { ["city"] = new JsonObject { ["type"] = "string" } },
        ["required"] = new JsonArray("city"),
    });

    private static JsonObject Order() => Tool(OrderTool, "Creates an order.", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["customer"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["name"] = new JsonObject { ["type"] = "string" },
                    ["address"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject { ["city"] = new JsonObject { ["type"] = "string" } },
                        ["required"] = new JsonArray("city"),
                    },
                },
                ["required"] = new JsonArray("name", "address"),
            },
            ["items"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject { ["sku"] = new JsonObject { ["type"] = "string" }, ["quantity"] = new JsonObject { ["type"] = "integer" } },
                    ["required"] = new JsonArray("sku", "quantity"),
                },
            },
        },
        ["required"] = new JsonArray("customer", "items"),
    });

    private static JsonObject Tool(string name, string description, JsonObject parameters) => new()
    {
        ["type"] = "function",
        ["function"] = new JsonObject { ["name"] = name, ["description"] = description, ["parameters"] = parameters },
    };
}
