using System.ClientModel.Primitives;

namespace CodeEditor.Modules.Agent.Services.Http;

/// <summary>Request header evaluated at send time: the conversation key changes with each new chat.</summary>
internal sealed class HeaderPolicy(string name, Func<string> value) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Set(name, value());
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Set(name, value());
        await ProcessNextAsync(message, pipeline, currentIndex);
    }
}
