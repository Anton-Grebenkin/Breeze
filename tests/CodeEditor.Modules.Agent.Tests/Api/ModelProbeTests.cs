using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Probes;
using CodeEditor.Modules.Agent.Services.Settings;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>
/// "Check the model" (ADR 0055) against gateways that behave differently: the report says what each probe found, and
/// the suggestions follow from it — the response limit field, images, where reasoning comes from.
/// </summary>
public sealed class ModelProbeTests
{
    private static readonly AgentConnection Connection = new() { Id = "work", Endpoint = "https://gateway.test/v1/", Headers = { ["X-Team"] = "shop" } };

    [Fact]
    public async Task CapableModel_PassesEveryProbe_AndIsDescribedByWhatItDid()
    {
        var gateway = new Gateway();

        var report = await RunAsync(gateway);

        Assert.Equal(
            [
                (ModelProbeKind.Answer, true, "отвечает"),
                (ModelProbeKind.OutputLimit, null, "поле max_completion_tokens"),
                (ModelProbeKind.ToolCall, true, "вызывает инструмент"),
                (ModelProbeKind.ParallelToolCalls, true, "два вызова в одном ответе"),
                (ModelProbeKind.NestedArguments, true, "приходят целыми"),
                (ModelProbeKind.Image, null, "видит изображения"),
                (ModelProbeKind.Reasoning, null, "приходят отдельным полем"),
                (ModelProbeKind.Language, true, "отвечает по-русски"),
            ],
            report.Steps.Select(step => (step.Kind, step.Passed, step.Detail)));
        Assert.Equal((true, ReasoningSource.Native, false), (report.Images, report.Reasoning, report.LegacyMaxTokens));

        // Every request goes to the connection with its key and headers, names the model and caps the reply.
        Assert.Equal(7, gateway.Requests.Count);
        Assert.All(gateway.Requests, request =>
        {
            Assert.Equal((new Uri("https://gateway.test/v1/chat/completions"), "Bearer work-key", "shop"), (request.Uri, request.Authorization, request.Team));
            Assert.Equal("team-pro", (string?)request.Body["model"]);
            Assert.InRange((int)request.Body["max_completion_tokens"]!, 1, 1024);
        });
    }

    [Fact]
    public async Task StrictServiceWithAPlainModel_IsFoundOut_FieldByField()
    {
        var gateway = new Gateway { IsStrict = true, CallsTools = false, SeesImages = false, Reasons = false, AnswersInRussian = false };

        var report = await RunAsync(gateway);

        Assert.Equal(
            [
                (ModelProbeKind.Answer, true, "отвечает"),
                (ModelProbeKind.OutputLimit, null, "сервис принимает только старое поле max_tokens"),
                (ModelProbeKind.ToolCall, false, "не вызвала инструмент: агент с этой моделью работать не сможет"),
                (ModelProbeKind.Image, null, "сервис не принял картинку: image input is not supported"),
                (ModelProbeKind.Reasoning, null, "сервис отклоняет поле reasoning_effort: оно не будет отправляться"),
                (ModelProbeKind.Language, false, "на вопрос по-русски ответила на другом языке"),
            ],
            report.Steps.Select(step => (step.Kind, step.Passed, step.Detail)));
        Assert.Equal((false, ReasoningSource.ThinkAloud, true), (report.Images, report.Reasoning, report.LegacyMaxTokens));

        // A model that calls no tool is not asked about two calls or nested arguments.
        Assert.DoesNotContain(gateway.Requests, request => request.Body.ToJsonString().Contains("create_order", StringComparison.Ordinal));
        Assert.All(gateway.Requests.Skip(1), request => Assert.NotNull(request.Body["max_tokens"]));
    }

    [Fact]
    public async Task ModelThatCallsOnce_AndLosesNestedFields_AndThinksInItsAnswer()
    {
        var gateway = new Gateway { CallsTwice = false, KeepsNested = false, ReasoningInline = true };

        var report = await RunAsync(gateway);

        Assert.Equal(
            [(ModelProbeKind.ParallelToolCalls, null), (ModelProbeKind.NestedArguments, false)],
            report.Steps.Where(step => step.Kind is ModelProbeKind.ParallelToolCalls or ModelProbeKind.NestedArguments).Select(step => (step.Kind, step.Passed)));
        Assert.Equal("приходят в тексте ответа, в теге think", report.Steps.Single(step => step.Kind == ModelProbeKind.Reasoning).Detail);
        Assert.Null(report.Reasoning);
    }

    [Fact]
    public async Task RejectedKey_EndsTheCheckAtTheFirstRequest()
    {
        var gateway = new Gateway { Rejects = true };

        var report = await RunAsync(gateway);

        var step = Assert.Single(report.Steps);
        Assert.Equal((ModelProbeKind.Answer, false, "не отвечает: invalid api key"), (step.Kind, step.Passed, step.Detail));
        Assert.Single(gateway.Requests);
        Assert.False(report.HasSuggestions);
    }

    [Fact]
    public async Task AnotherProtocol_IsNotProbed()
    {
        var gateway = new Gateway();
        var probe = new ModelProbe(new HttpClientPipelineTransport(new HttpClient(gateway)));

        var report = await probe.RunAsync(new AgentConnection { Id = "c", Endpoint = "https://c.test/v1", Api = AgentApi.Messages }, "key", "claude", TestContext.Current.CancellationToken);

        Assert.Equal("Проверка идёт через Chat Completions, а у подключения другой протокол.", Assert.Single(report.Steps).Detail);
        Assert.Empty(gateway.Requests);
    }

    private static Task<ModelProbeReport> RunAsync(Gateway gateway) =>
        new ModelProbe(new HttpClientPipelineTransport(new HttpClient(gateway))).RunAsync(Connection, "work-key", "team-pro", TestContext.Current.CancellationToken);

    private sealed record Request(Uri? Uri, string? Authorization, string? Team, JsonNode Body);

    /// <summary>A gateway whose model behaves as the switches say; it answers by what the request asks.</summary>
    private sealed class Gateway : HttpMessageHandler
    {
        public List<Request> Requests { get; } = [];

        /// <summary>Unknown fields mean 400: <c>max_completion_tokens</c> and <c>reasoning_effort</c>.</summary>
        public bool IsStrict { get; init; }

        public bool Rejects { get; init; }

        public bool CallsTools { get; init; } = true;

        public bool CallsTwice { get; init; } = true;

        public bool KeepsNested { get; init; } = true;

        public bool SeesImages { get; init; } = true;

        public bool Reasons { get; init; } = true;

        public bool ReasoningInline { get; init; }

        public bool AnswersInRussian { get; init; } = true;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            Requests.Add(new Request(request.RequestUri, request.Headers.Authorization?.ToString(), request.Headers.TryGetValues("X-Team", out var team) ? string.Join(",", team) : null, body));
            if (Rejects)
            {
                return Error(HttpStatusCode.Unauthorized, "invalid api key");
            }

            if (IsStrict && (body["max_completion_tokens"] is not null || body["reasoning_effort"] is not null))
            {
                return Error(HttpStatusCode.BadRequest, "unsupported parameter");
            }

            var text = body["messages"]![0]!["content"] is JsonValue value ? value.GetValue<string>() : string.Empty;
            if (body["messages"]![0]!["content"] is JsonArray)
            {
                return SeesImages ? Message("Red.") : Error(HttpStatusCode.BadRequest, "image input is not supported");
            }

            if (body["tools"] is not null)
            {
                return CallsTools ? Message(null, calls: Calls(text)) : Message("The weather in Paris is sunny.");
            }

            if (text.Contains("sheep", StringComparison.Ordinal))
            {
                return ReasoningInline ? Message("<think>17 + 23 - 9</think>31")
                    : Reasons ? Message("31", reasoning: "17 + 23 = 40; 40 - 9 = 31.")
                    : Message("31");
            }

            return Message(text.Contains("рекурсия", StringComparison.Ordinal) && AnswersInRussian ? "Рекурсия — это вызов функцией самой себя." : "OK, recursion is a function calling itself.");
        }

        private object[] Calls(string question)
        {
            if (question.Contains("create_order", StringComparison.Ordinal))
            {
                var arguments = KeepsNested
                    ? """{"customer":{"name":"Anna Petrova","address":{"city":"Paris"}},"items":[{"sku":"A-1","quantity":2},{"sku":"B-7","quantity":1}]}"""
                    : """{"customer":"Anna Petrova, Paris","items":"A-1 x2, B-7 x1"}""";
                return [Call("create_order", arguments)];
            }

            return question.Contains("Rome", StringComparison.Ordinal) && CallsTwice
                ? [Call("get_weather", """{"city":"Paris"}"""), Call("get_weather", """{"city":"Rome"}""")]
                : [Call("get_weather", """{"city":"Paris"}""")];
        }

        private static object Call(string name, string arguments) => new { id = "call_1", type = "function", function = new { name, arguments } };

        private static HttpResponseMessage Message(string? content, string? reasoning = null, object[]? calls = null)
        {
            var message = new Dictionary<string, object?> { ["role"] = "assistant", ["content"] = content };
            if (reasoning is not null)
            {
                message["reasoning_content"] = reasoning;
            }

            if (calls is not null)
            {
                message["tool_calls"] = calls;
            }

            return Json(HttpStatusCode.OK, new { choices = new[] { new { index = 0, message, finish_reason = calls is null ? "stop" : "tool_calls" } } });
        }

        private static HttpResponseMessage Error(HttpStatusCode status, string message) => Json(status, new { error = new { message } });

        private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
            new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    }
}
