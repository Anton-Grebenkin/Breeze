using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using System.Text.Json;
using CodeEditor.Modules.Agent.Resources;
using OpenAI;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// <c>GET /models</c> of an OpenAI-compatible service, read as raw JSON: besides the ids, gateways say different things
/// about a model under different names (OpenRouter <c>context_length</c>, vLLM <c>max_model_len</c>), and the library
/// drops all of it.
/// </summary>
/// <param name="transport">Transport; a fake server in tests.</param>
public sealed class ConnectionProbe(PipelineTransport? transport = null) : IConnectionProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private static readonly string[] ListFields = ["data", "models"];
    private static readonly string[] IdFields = ["id", "name", "model"];
    private static readonly string[] WindowFields = ["context_length", "context_window", "max_model_len", "max_context_length", "max_input_tokens"];

    public async Task<IReadOnlyList<ServiceModel>> ListModelsAsync(AgentConnection connection, string? key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!Uri.TryCreate(connection.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https"))
        {
            throw new AgentConfigurationException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorEndpointNotUrl, connection.Endpoint));
        }

        var pipeline = Pipeline(key);
        using var message = pipeline.CreateMessage();
        message.Apply(new RequestOptions { CancellationToken = cancellationToken });
        message.Request.Method = "GET";
        message.Request.Uri = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/models");
        foreach (var (name, value) in connection.Headers)
        {
            message.Request.Headers.Set(name, value);
        }

        await pipeline.SendAsync(message);
        var response = message.Response ?? throw new InvalidOperationException("The pipeline returned no response.");
        if (response.IsError)
        {
            throw await ClientResultException.CreateAsync(response);
        }

        using var document = JsonDocument.Parse(response.Content.ToMemory());
        return Parse(document.RootElement);
    }

    /// <summary>The list is <c>data</c> (OpenAI), <c>models</c> (Ollama) or the answer itself.</summary>
    /// <exception cref="JsonException">There is no list in the answer.</exception>
    internal static IReadOnlyList<ServiceModel> Parse(JsonElement root)
    {
        var list = root.ValueKind == JsonValueKind.Array ? root : ListFields
            .Select(field => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(field, out var value) ? value : default)
            .FirstOrDefault(static value => value.ValueKind == JsonValueKind.Array);
        if (list.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The answer has no model list.");
        }

        List<ServiceModel> models = [];
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && IdFields.Select(field => Text(item, field)).FirstOrDefault(static id => id is not null) is { } id)
            {
                models.Add(new ServiceModel(id, WindowFields.Select(field => Number(item, field)).FirstOrDefault(static tokens => tokens is not null), Images(item)));
            }
        }

        return models;
    }

    // OpenRouter: architecture.input_modalities ["text","image"]; LiteLLM and others: supports_vision or capabilities.vision.
    private static bool? Images(JsonElement model)
    {
        if (model.TryGetProperty("architecture", out var architecture) && architecture.ValueKind == JsonValueKind.Object
            && architecture.TryGetProperty("input_modalities", out var inputs) && inputs.ValueKind == JsonValueKind.Array)
        {
            return inputs.EnumerateArray().Any(static input => input.ValueKind == JsonValueKind.String && input.GetString() == "image");
        }

        return Flag(model, "supports_vision")
            ?? (model.TryGetProperty("capabilities", out var capabilities) && capabilities.ValueKind == JsonValueKind.Object ? Flag(capabilities, "vision") : null);
    }

    private static bool? Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    private static int? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0 ? number : null;

    private ClientPipeline Pipeline(string? key)
    {
        var options = new OpenAIClientOptions { NetworkTimeout = Timeout, RetryPolicy = new ClientRetryPolicy(maxRetries: 0) };
        options.AddPolicy(new ErrorBodyPolicy(), PipelinePosition.PerCall);
        if (transport is not null)
        {
            options.Transport = transport;
        }

        PipelinePolicy[] authentication = string.IsNullOrEmpty(key) ? [] : [ApiKeyAuthenticationPolicy.CreateBearerAuthorizationPolicy(new ApiKeyCredential(key))];
        return ClientPipeline.Create(options, authentication, [], []);
    }
}
