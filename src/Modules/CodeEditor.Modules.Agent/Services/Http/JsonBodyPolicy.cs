using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Http;

/// <summary>
/// Edits the JSON request body before sending, to pass fields the library lacks (Claude cache marks and reasoning,
/// GPT reply <c>phase</c>). The rewrite returns <c>true</c> if it changed anything; otherwise the body is sent as is.
/// </summary>
internal sealed class JsonBodyPolicy(Func<JsonNode, bool> rewrite) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Rewrite(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Rewrite(message);
        await ProcessNextAsync(message, pipeline, currentIndex);
    }

    private void Rewrite(PipelineMessage message)
    {
        if (message.Request.Content is not { } content || message.Request.Method != "POST")
        {
            return;
        }

        using var buffer = new MemoryStream();
        content.WriteTo(buffer);
        if (JsonNode.Parse(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)) is { } body && rewrite(body))
        {
            message.Request.Content = BinaryContent.Create(BinaryData.FromString(body.ToJsonString()));
        }
    }
}
