using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Models;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Configuration;

namespace CodeEditor.Modules.Agent.Tests.Models;

/// <summary>
/// The connections panel (ADR 0055): a gateway is added without editing JSON — the address is checked, the key goes
/// to the secret store, the models of the service are switched on and described.
/// </summary>
public sealed class ConnectionsTests : IDisposable
{
    private const string GatewayKey = "agent-api-key-connection-gateway";

    private readonly FakeSettingsService _settings = new();
    private readonly TestOptionsMonitor<AgentOptions> _options = new(new AgentOptions());
    private readonly AgentFixture.MemorySecrets _secrets = new();
    private readonly FakeProbe _probe = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly StatusBarViewModel _statusBar = new();
    private readonly ConnectionsViewModel _panel;
    private string _file = string.Empty;

    public ConnectionsTests()
    {
        // Written values go through the real settings text and binder, as in the app.
        _settings.Changed += (_, _) => Reload();
        var store = new ConnectionStore(_options, _settings, _secrets);
        _panel = new ConnectionsViewModel(new ConnectionServices(store, _probe, _dialogs, _settings, _options, _statusBar));
    }

    public void Dispose() => _panel.Dispose();

    [Fact]
    public async Task Add_ChecksTheAddress_SavesTheConnection_KeepsTheKeyApart_AndShowsItsModels()
    {
        _probe.Models = [new ServiceModel("hosted_vllm/team-pro", 64_000), new ServiceModel("hosted_vllm/team-fast")];

        await AddGatewayAsync();

        var connection = Assert.Single(_options.CurrentValue.Connections);
        Assert.Equal(("gateway", "Рабочий шлюз", "https://gateway.test/v1"), (connection.Id, connection.Name, connection.Endpoint));
        Assert.Equal(("secret-key", "secret-key"), (_secrets.Get(GatewayKey), _probe.Keys[0]));
        Assert.DoesNotContain("secret-key", _file, StringComparison.Ordinal);
        Assert.DoesNotContain("keyless", _file, StringComparison.Ordinal);

        var row = Assert.Single(_panel.Connections);
        Assert.True(row.IsExpanded);
        Assert.Equal(("Рабочий шлюз", "ключ задан"), (row.Title, row.KeyText));
        Assert.Equal(["hosted_vllm/team-fast", "hosted_vllm/team-pro"], row.Models.Select(model => model.Id));
        Assert.Equal((string.Empty, string.Empty, string.Empty), (_panel.Name, _panel.Endpoint, _panel.Key));
        Assert.Equal("Подключение «Рабочий шлюз» добавлено, моделей: 2. Включите нужные.", _panel.Status);
    }

    [Fact]
    public async Task SwitchedOnModel_IsInThePicker_DescribedAsWhatItIs_AndBecomesTheConversationModel()
    {
        _probe.Models = [new ServiceModel("hosted_vllm/team-pro", 64_000), new ServiceModel("hosted_vllm/team-fast")];
        await AddGatewayAsync();
        var model = _panel.Connections[0].Models.Single(row => row.Id == "hosted_vllm/team-pro");

        model.IsEnabled = true;
        await model.FamilyMenu.Single(item => item.Header == "DeepSeek").Command!.ExecuteAsync(null);
        model.Name = "DeepSeek, старшая";

        var description = Assert.Single(_options.CurrentValue.Connections[0].Models);
        Assert.Equal(("hosted_vllm/team-pro", "DeepSeek, старшая", ModelFamily.DeepSeek, 64_000), (description.Id, description.Name, description.Family, description.ContextWindow));
        Assert.Equal("gateway:hosted_vllm/team-pro", ModelCatalog.Choices(_options.CurrentValue)[0]);
        Assert.Equal("Моделей: 2, в списке выбора: 1", _panel.Connections[0].Status);

        model.UseCommand.Execute(null);

        Assert.Equal("gateway:hosted_vllm/team-pro", _options.CurrentValue.Model);
        Assert.Equal(ModelFamily.DeepSeek, ModelProfiles.For(_options.CurrentValue).Family);
        Assert.Equal(("Модель агента: DeepSeek, старшая", true), (_statusBar.Message, model.IsCurrent));
    }

    [Fact]
    public async Task FailedCheck_SaysWhy_TheConnectionCanBeAddedAnyway_AndAModelByItsId()
    {
        _probe.Failure = new JsonException("not a list");
        _panel.Endpoint = "http://localhost:8000/v1";
        _panel.IsKeyless = true;

        await _panel.AddCommand.ExecuteAsync(null);

        Assert.Empty(_options.CurrentValue.Connections);
        Assert.Equal(("Проверка не прошла. Ответ сервиса — не список моделей.", true), (_panel.Status, _panel.CanSkipCheck));

        _panel.AddUncheckedCommand.Execute(null);

        var connection = Assert.Single(_options.CurrentValue.Connections);
        Assert.Equal(("localhost", true, "http://localhost:8000/v1"), (connection.Id, connection.Keyless, connection.Endpoint));
        var row = Assert.Single(_panel.Connections);
        Assert.Equal("ключ не нужен", row.KeyText);

        row.Filter = "qwen3-coder";
        row.AddModelCommand.Execute(null);

        Assert.Equal("qwen3-coder", Assert.Single(_options.CurrentValue.Connections[0].Models).Id);
        Assert.Null(_probe.Keys[^1]);
    }

    [Fact]
    public async Task Key_IsSetFromTheRow_Removal_AsksFirst_AndTakesTheKeyAway()
    {
        await AddGatewayAsync();
        var row = _panel.Connections[0];

        _dialogs.SecretAnswer = "new-key";
        row.SetKeyCommand.Execute(null);
        Assert.Equal("new-key", _secrets.Get(GatewayKey));

        _dialogs.ConfirmAnswer = false;
        row.RemoveCommand.Execute(null);
        Assert.Single(_panel.Connections);

        _dialogs.ConfirmAnswer = true;
        row.RemoveCommand.Execute(null);

        Assert.Equal("Удалить подключение «Рабочий шлюз»?", _dialogs.Confirmations[^1]);
        Assert.Empty(_panel.Connections);
        Assert.Empty(_options.CurrentValue.Connections);
        Assert.Null(_secrets.Get(GatewayKey));
        Assert.False(_panel.HasConnections);
    }

    [Theory]
    [InlineData("Work gateway", "https://x.test/v1", "work-gateway")]
    [InlineData("Рабочий шлюз", "https://gateway.example/v1", "gateway")]
    [InlineData("", "http://localhost:11434/v1", "localhost")]
    [InlineData("", "не адрес", "connection")]
    public void NewId_ComesFromTheNameOrTheHost(string name, string endpoint, string id) =>
        Assert.Equal(id, new ConnectionStore(_options, _settings, _secrets).NewId(name, endpoint));

    [Fact]
    public async Task NewId_IsFree()
    {
        await AddGatewayAsync();

        Assert.Equal("gateway-2", new ConnectionStore(_options, _settings, _secrets).NewId("Gateway", "https://other.test/v1"));
    }

    [Theory]
    [InlineData("""{"object":"list","data":[{"id":"gpt-x"},{"id":"team","context_length":200000,"architecture":{"input_modalities":["text","image"]}}]}""", "gpt-x,team", 200_000, true)]
    [InlineData("""{"data":[{"id":"qwen","max_model_len":32768,"supports_vision":false}]}""", "qwen", 32_768, false)]
    [InlineData("""{"models":[{"name":"llama3:8b"}]}""", "llama3:8b", null, null)]
    [InlineData("""[{"id":"a","capabilities":{"vision":true}},{"no":"id"}]""", "a", null, true)]
    public void ModelList_IsReadFromWhateverTheServiceSays(string json, string ids, int? window, bool? images)
    {
        using var document = JsonDocument.Parse(json);

        var models = ConnectionProbe.Parse(document.RootElement);

        Assert.Equal(ids, string.Join(',', models.Select(model => model.Id)));
        Assert.Equal((window, images), (models[^1].ContextWindow, models[^1].Images));
    }

    [Fact]
    public void AnswerWithoutAList_IsNotAModelList()
    {
        using var document = JsonDocument.Parse("""{"error":"nope"}""");

        Assert.Throws<JsonException>(() => ConnectionProbe.Parse(document.RootElement));
    }

    [Theory]
    [InlineData("secret", "Bearer secret")]
    [InlineData(null, null)]
    public async Task ModelListRequest_GoesToModelsOfTheAddress_WithTheKeyAndHeaders(string? key, string? authorization)
    {
        var handler = new ListHandler();
        var probe = new ConnectionProbe(new HttpClientPipelineTransport(new HttpClient(handler)));
        var connection = new AgentConnection { Id = "c", Endpoint = "https://gateway.test/v1/", Headers = { ["X-Team"] = "shop" } };

        var models = await probe.ListModelsAsync(connection, key, TestContext.Current.CancellationToken);

        Assert.Equal("team", Assert.Single(models).Id);
        Assert.Equal((new Uri("https://gateway.test/v1/models"), authorization, "shop"), (handler.Uri, handler.Authorization, handler.Team));
    }

    private async Task AddGatewayAsync()
    {
        (_panel.Name, _panel.Endpoint, _panel.Key) = ("Рабочий шлюз", "gateway.test/v1", " secret-key ");
        await _panel.AddCommand.ExecuteAsync(null);
    }

    private void Reload()
    {
        _file = string.Empty;
        foreach (var (key, value) in _settings.Written)
        {
            if (value is not null)
            {
                _file = SettingsJson.SetValue(_file, key, value);
            }
        }

        var options = new AgentOptions();
        new ConfigurationBuilder().AddInMemoryCollection(SettingsJson.Parse(_file)).Build().GetSection(AgentOptions.Section).Bind(options);
        _options.Set(options);
    }

    private sealed class FakeProbe : IConnectionProbe
    {
        public IReadOnlyList<ServiceModel> Models { get; set; } = [];

        public Exception? Failure { get; set; }

        public List<string?> Keys { get; } = [];

        public Task<IReadOnlyList<ServiceModel>> ListModelsAsync(AgentConnection connection, string? key, CancellationToken cancellationToken)
        {
            Keys.Add(key);
            return Failure is null ? Task.FromResult(Models) : Task.FromException<IReadOnlyList<ServiceModel>>(Failure);
        }
    }

    private sealed class ListHandler : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }

        public string? Authorization { get; private set; }

        public string? Team { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            Team = request.Headers.TryGetValues("X-Team", out var values) ? string.Join(",", values) : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"data":[{"id":"team"}]}""") });
        }
    }
}
