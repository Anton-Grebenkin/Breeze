using System.ClientModel;
using CodeEditor.Core.Storage;
using OpenAI;
using OpenAI.Models;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Lists models via the OpenAI client with the same key, endpoint and pipeline as chat
/// (<see cref="OpenAIChatClientFactory"/>).
/// </summary>
public sealed class OpenAIModelListClient(ISecretStore secrets) : IModelListClient
{
    public async Task<IReadOnlyList<string>> ListAsync(AgentOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var (key, endpoint) = OpenAIChatClientFactory.Connection(options, secrets);
        var client = new OpenAIModelClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
        var models = await client.GetModelsAsync(cancellationToken);
        return [.. models.Value.Select(static model => model.Id)];
    }
}
