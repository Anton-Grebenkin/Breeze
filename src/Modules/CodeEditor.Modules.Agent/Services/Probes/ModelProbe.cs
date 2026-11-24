using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Resources;
using OpenAI;

namespace CodeEditor.Modules.Agent.Services.Probes;

/// <summary>
/// The model check over Chat Completions, without streaming: the answer and the field of the response limit first,
/// then tool calls, an image, reasoning and Russian. A model that does not answer ends the check; one that calls no
/// tool is not asked about two calls or nested arguments.
/// </summary>
/// <param name="transport">Transport; a fake server in tests.</param>
public sealed class ModelProbe(PipelineTransport? transport = null) : IModelProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    private const string InlineReasoningTag = "<think";
    private const string ExpectedCity = "paris";
    private const int ExpectedItems = 2;

    // The Cyrillic block of Unicode.
    private const char CyrillicFirst = '\u0400';
    private const char CyrillicLast = '\u04FF';

    public async Task<ModelProbeReport> RunAsync(AgentConnection connection, string? key, string model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (connection.Api is AgentApi.Responses or AgentApi.Messages || !Uri.TryCreate(connection.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return new ModelProbeReport([new ModelProbeStep(ModelProbeKind.Answer, false, Strings.ProbeProtocol)]);
        }

        var service = new Service(Pipeline(key), new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/chat/completions"), connection.Headers, cancellationToken);
        List<ModelProbeStep> steps = [];
        var limitField = ModelProbeRequests.NewLimitField;
        var answer = await SendAsync(service, ModelProbeRequests.Answer(model, limitField));
        if (!answer.IsAnswer && answer.Status == 400)
        {
            limitField = ModelProbeRequests.LegacyLimitField;
            answer = await SendAsync(service, ModelProbeRequests.Answer(model, limitField));
        }

        if (!answer.IsAnswer)
        {
            return new ModelProbeReport([new ModelProbeStep(ModelProbeKind.Answer, false, Format(Strings.ProbeNoAnswer, answer.Error))]);
        }

        var legacy = limitField == ModelProbeRequests.LegacyLimitField;
        steps.Add(new ModelProbeStep(ModelProbeKind.Answer, true, Strings.ProbeAnswers));
        steps.Add(new ModelProbeStep(ModelProbeKind.OutputLimit, null, legacy ? Strings.ProbeLimitLegacy : Strings.ProbeLimitNew));
        if (await ToolCallAsync(service, model, limitField, steps))
        {
            steps.Add(TwoCalls(await SendAsync(service, ModelProbeRequests.TwoToolCalls(model, limitField))));
            steps.Add(Nested(await SendAsync(service, ModelProbeRequests.NestedArguments(model, limitField))));
        }

        var (image, images) = Image(await SendAsync(service, ModelProbeRequests.Image(model, limitField)));
        steps.Add(image);
        var (reasoning, source) = await ReasoningAsync(service, model, limitField);
        steps.Add(reasoning);
        steps.Add(Russian(await SendAsync(service, ModelProbeRequests.Russian(model, limitField))));
        return new ModelProbeReport(steps) { Images = images, Reasoning = source, LegacyMaxTokens = legacy };
    }

    private static async Task<bool> ToolCallAsync(Service service, string model, string limitField, List<ModelProbeStep> steps)
    {
        var reply = await SendAsync(service, ModelProbeRequests.ToolCall(model, limitField));
        var call = reply.Calls.FirstOrDefault(static call => call.Name == ModelProbeRequests.WeatherTool);
        using var arguments = call?.Parse();
        steps.Add(!reply.IsAnswer ? Failed(ModelProbeKind.ToolCall, reply)
            : call is null ? new ModelProbeStep(ModelProbeKind.ToolCall, false, Strings.ProbeToolNotCalled)
            : arguments is null ? new ModelProbeStep(ModelProbeKind.ToolCall, false, Strings.ProbeToolArgumentsBroken)
            : new ModelProbeStep(ModelProbeKind.ToolCall, true, Strings.ProbeToolCalled));
        return arguments is not null;
    }

    private static ModelProbeStep TwoCalls(ModelProbeReply reply) =>
        !reply.IsAnswer ? Failed(ModelProbeKind.ParallelToolCalls, reply)
        : reply.Calls.Count(static call => call.Name == ModelProbeRequests.WeatherTool) >= 2 ? new ModelProbeStep(ModelProbeKind.ParallelToolCalls, true, Strings.ProbeTwoCalls)
        : new ModelProbeStep(ModelProbeKind.ParallelToolCalls, null, Strings.ProbeOneCall);

    private static ModelProbeStep Nested(ModelProbeReply reply)
    {
        if (!reply.IsAnswer)
        {
            return Failed(ModelProbeKind.NestedArguments, reply);
        }

        using var arguments = reply.Calls.FirstOrDefault(static call => call.Name == ModelProbeRequests.OrderTool)?.Parse();
        var whole = arguments is not null
            && arguments.RootElement.TryGetProperty("customer", out var customer) && customer.ValueKind == JsonValueKind.Object
            && customer.TryGetProperty("address", out var address) && address.ValueKind == JsonValueKind.Object
            && address.TryGetProperty("city", out var city) && city.ValueKind == JsonValueKind.String
            && city.GetString()!.Contains(ExpectedCity, StringComparison.OrdinalIgnoreCase)
            && arguments.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() == ExpectedItems
            && items.EnumerateArray().All(static item => item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("sku", out var sku) && sku.ValueKind == JsonValueKind.String
                && item.TryGetProperty("quantity", out var quantity) && quantity.ValueKind == JsonValueKind.Number);
        return new ModelProbeStep(ModelProbeKind.NestedArguments, whole, whole ? Strings.ProbeNested : Strings.ProbeNestedLost);
    }

    // A service that refuses the picture and a model that cannot tell its colour both mean "no images".
    private static (ModelProbeStep Step, bool Images) Image(ModelProbeReply reply)
    {
        var left = reply.Content.IndexOf(ModelProbeRequests.ImageLeftColour, StringComparison.OrdinalIgnoreCase);
        var seen = reply.IsAnswer && left >= 0 && reply.Content.IndexOf(ModelProbeRequests.ImageRightColour, left, StringComparison.OrdinalIgnoreCase) > left;
        var detail = seen ? Strings.ProbeImageSeen : reply.IsAnswer ? Strings.ProbeImageNotSeen : Format(Strings.ProbeImageRefused, reply.Error);
        return (new ModelProbeStep(ModelProbeKind.Image, null, detail), seen);
    }

    // A strict service answers 400 to reasoning_effort: then the field must not be sent, which "think aloud" ensures.
    private static async Task<(ModelProbeStep Step, ReasoningSource? Source)> ReasoningAsync(Service service, string model, string limitField)
    {
        var reply = await SendAsync(service, ModelProbeRequests.Reasoning(model, limitField, withEffort: true));
        if (!reply.IsAnswer && reply.Status == 400)
        {
            var plain = await SendAsync(service, ModelProbeRequests.Reasoning(model, limitField, withEffort: false));
            return plain.IsAnswer
                ? (new ModelProbeStep(ModelProbeKind.Reasoning, null, Strings.ProbeReasoningEffortRefused), ReasoningSource.ThinkAloud)
                : (Failed(ModelProbeKind.Reasoning, plain), null);
        }

        if (!reply.IsAnswer)
        {
            return (Failed(ModelProbeKind.Reasoning, reply), null);
        }

        return reply.Reasoning.Length > 0 ? (new ModelProbeStep(ModelProbeKind.Reasoning, null, Strings.ProbeReasoningField), ReasoningSource.Native)
            : reply.Content.Contains(InlineReasoningTag, StringComparison.OrdinalIgnoreCase) ? (new ModelProbeStep(ModelProbeKind.Reasoning, null, Strings.ProbeReasoningInline), null)
            : (new ModelProbeStep(ModelProbeKind.Reasoning, null, Strings.ProbeReasoningNone), ReasoningSource.ThinkAloud);
    }

    private static ModelProbeStep Russian(ModelProbeReply reply)
    {
        if (!reply.IsAnswer)
        {
            return Failed(ModelProbeKind.Language, reply);
        }

        var letters = reply.Content.Count(char.IsLetter);
        var cyrillic = reply.Content.Count(static character => character is >= CyrillicFirst and <= CyrillicLast);
        var russian = letters > 0 && cyrillic * 2 > letters;
        return new ModelProbeStep(ModelProbeKind.Language, russian, russian ? Strings.ProbeRussian : Strings.ProbeNotRussian);
    }

    private static ModelProbeStep Failed(ModelProbeKind kind, ModelProbeReply reply) => new(kind, false, Format(Strings.ProbeFailed, reply.Error));

    // A request that got no answer at all is a refusal like any other: the report says why and the check goes on.
    private static async Task<ModelProbeReply> SendAsync(Service service, JsonObject body)
    {
        try
        {
            using var message = service.Pipeline.CreateMessage();
            message.Apply(new RequestOptions { CancellationToken = service.Cancellation });
            message.Request.Method = "POST";
            message.Request.Uri = service.Uri;
            message.Request.Headers.Set("Content-Type", "application/json");
            foreach (var (name, value) in service.Headers)
            {
                message.Request.Headers.Set(name, value);
            }

            message.Request.Content = BinaryContent.Create(BinaryData.FromString(body.ToJsonString()));
            await service.Pipeline.SendAsync(message);
            var response = message.Response ?? throw new InvalidOperationException("The pipeline returned no response.");
            return Read(response);
        }
        catch (Exception exception) when (exception is not OperationCanceledException && ConnectionErrors.IsExpected(exception))
        {
            return ModelProbeReply.Refused(0, ConnectionErrors.Describe(exception, service.Uri.GetLeftPart(UriPartial.Authority)));
        }
    }

    // An error page of a proxy is not JSON: the status says what happened.
    private static ModelProbeReply Read(PipelineResponse response)
    {
        try
        {
            using var document = JsonDocument.Parse(response.Content.ToMemory());
            return response.IsError
                ? ModelProbeReply.Refused(response.Status, ModelProbeReply.ErrorOf(document.RootElement) is { Length: > 0 } error ? error : Format(Strings.ProbeStatus, response.Status))
                : ModelProbeReply.Parse(response.Status, document.RootElement);
        }
        catch (JsonException)
        {
            return ModelProbeReply.Refused(response.Status, response.IsError ? Format(Strings.ProbeStatus, response.Status) : Strings.ProbeNotJson);
        }
    }

    private ClientPipeline Pipeline(string? key)
    {
        var options = new OpenAIClientOptions { NetworkTimeout = Timeout, RetryPolicy = new ClientRetryPolicy(maxRetries: 0) };
        if (transport is not null)
        {
            options.Transport = transport;
        }

        PipelinePolicy[] authentication = string.IsNullOrEmpty(key) ? [] : [ApiKeyAuthenticationPolicy.CreateBearerAuthorizationPolicy(new ApiKeyCredential(key))];
        return ClientPipeline.Create(options, authentication, [], []);
    }

    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.CurrentCulture, format, values);

    private sealed record Service(ClientPipeline Pipeline, Uri Uri, IReadOnlyDictionary<string, string> Headers, CancellationToken Cancellation);
}
