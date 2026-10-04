using System.ClientModel.Primitives;

namespace CodeEditor.Modules.Agent.Services.Http;

/// <summary>
/// Buffers error response bodies. A streaming request does not buffer the response, so the library exception carried
/// only the status: Provod explains a 403 in the body (<c>FIRST_TOP_UP_REQUIRED</c>) while the user saw "key rejected".
/// With the body buffered the exception exposes the service's text (<see cref="ServiceErrorText"/>).
/// </summary>
internal sealed class ErrorBodyPolicy : PipelinePolicy
{
    private const int FirstErrorStatus = 400;

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ProcessNext(message, pipeline, currentIndex);
        if (message.Response is { Status: >= FirstErrorStatus } response)
        {
            response.BufferContent();
        }
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        await ProcessNextAsync(message, pipeline, currentIndex);
        if (message.Response is { Status: >= FirstErrorStatus } response)
        {
            await response.BufferContentAsync();
        }
    }
}
