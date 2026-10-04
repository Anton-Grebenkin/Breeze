using System.Text;
using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>
/// Chat Completions stream built from <see cref="FakeModelServer"/> response items, as ProxyAPI sends it: the role in
/// the first chunk, reasoning in the <c>reasoning</c> field (as Grok sends it), one chunk per tool call.
/// </summary>
internal static class CompletionsStream
{
    public static string Write(string id, JsonObject[] items)
    {
        var stream = new StringBuilder();
        void Chunk(JsonObject delta, string? finish = null)
        {
            var chunk = new JsonObject
            {
                ["id"] = id,
                ["object"] = "chat.completion.chunk",
                ["created"] = 1_760_000_000,
                ["model"] = "model",
                ["choices"] = new JsonArray(new JsonObject { ["index"] = 0, ["delta"] = delta, ["finish_reason"] = finish }),
            };
            if (finish is not null)
            {
                chunk["usage"] = new JsonObject { ["prompt_tokens"] = 100, ["completion_tokens"] = 10, ["total_tokens"] = 110 };
            }

            stream.Append("data: ").Append(chunk.ToJsonString()).Append("\n\n");
        }

        Chunk(new JsonObject { ["role"] = "assistant", ["content"] = "" });
        var calls = 0;
        foreach (var item in items)
        {
            Chunk((string?)item["type"] switch
            {
                "reasoning" => new JsonObject { ["content"] = "", ["reasoning"] = item["summary"]![0]!["text"]!.DeepClone() },
                "message" => new JsonObject { ["content"] = item["content"]![0]!["text"]!.DeepClone() },
                _ => new JsonObject { ["tool_calls"] = new JsonArray(ToolCall(calls++, item)) },
            });
        }

        Chunk(new JsonObject(), calls > 0 ? "tool_calls" : "stop");
        stream.Append("data: [DONE]\n\n");
        return stream.ToString();
    }

    private static JsonObject ToolCall(int index, JsonObject item) => new()
    {
        ["index"] = index,
        ["id"] = item["call_id"]!.DeepClone(),
        ["type"] = "function",
        ["function"] = new JsonObject { ["name"] = item["name"]!.DeepClone(), ["arguments"] = item["arguments"]!.DeepClone() },
    };
}
