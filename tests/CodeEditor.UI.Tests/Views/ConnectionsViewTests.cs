using CodeEditor.Core.Settings;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.ViewModels.Models;
using CodeEditor.Modules.Agent.Wpf.Views;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using CodeEditor.UI.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace CodeEditor.UI.Tests.Views;

/// <summary>
/// The connections panel as the user gets it (ADR 0055): the real view with the app's theme, operated through its
/// controls without a mouse. A gateway is added from the form, its model is switched on, described and chosen.
/// </summary>
public sealed class ConnectionsViewTests : IDisposable
{
    private readonly OffScreenView _view = new();
    private readonly FakeSettingsService _settings = new();
    private readonly TestOptionsMonitor<AgentOptions> _options = new(new AgentOptions());
    private readonly Secrets _secrets = new();
    private readonly Probe _probe = new();

    public ConnectionsViewTests() => _settings.Changed += (_, _) => Reload();

    public void Dispose() => _view.Dispose();

    [Fact]
    public void Gateway_IsAddedFromTheForm_ItsModelIsSwitchedOn_Described_AndChosen()
    {
        var services = new ConnectionServices(new ConnectionStore(_options, _settings, _secrets), _probe, new FakeDialogs(), _settings, _options, new StatusBarViewModel());
        _view.Show(() => new ConnectionsView { DataContext = new ConnectionsViewModel(services) });
        Assert.True(_view.IsShown("Agent.Connections.Empty"));
        Assert.False(_view.IsShown("Agent.Connections.New.AddUnchecked"));

        _view.Type("Agent.Connections.New.Name", "Gateway");
        _view.Type("Agent.Connections.New.Endpoint", "gateway.test/v1");
        _view.Type("Agent.Connections.New.Key", "test-key");
        _view.Press("Agent.Connections.New.Add");

        // The connection is saved, the key is apart, and the service's models are listed under it.
        Assert.Equal(("https://gateway.test/v1", "test-key"), (_options.CurrentValue.Connections.Single().Endpoint, _secrets.Get("agent-api-key-connection-gateway")));
        Assert.False(_view.IsShown("Agent.Connections.Empty"));
        Assert.Equal("Моделей: 2, в списке выбора: 0", _view.TextOf("Agent.Connections.Status.gateway"));
        Assert.Equal(string.Empty, _view.TextOf("Agent.Connections.New.Name"));
        Assert.False(_view.IsShown("Agent.Connections.Family.team-pro"));

        // Switched on, the model shows what can be said about it; the family menu lists the families.
        _view.Press("Agent.Connections.Model.team-pro");
        Assert.True(_view.IsShown("Agent.Connections.Family.team-pro"));
        Assert.Equal("64000", _view.TextOf("Agent.Connections.Window.team-pro"));
        _view.Press("Agent.Connections.Family.team-pro");
        Assert.Equal(["По имени", "Claude", "GPT с рассуждениями", "GPT-4 и старше", "Gemini", "DeepSeek", "Qwen", "Другие сильные", "Простые и локальные"], _view.MenuOf("Agent.Connections.Family.team-pro"));

        _view.Type("Agent.Connections.Name.team-pro", "Старшая");
        _view.SaveScreenshot("connections-view");
        _view.Press("Agent.Connections.Use.team-pro");

        Assert.Equal("gateway:team-pro", _options.CurrentValue.Model);
        Assert.Equal(("team-pro", "Старшая", 64_000), (_options.CurrentValue.Connections[0].Models[0].Id, _options.CurrentValue.Connections[0].Models[0].Name, _options.CurrentValue.Connections[0].Models[0].ContextWindow));
        Assert.False(_view.IsShown("Agent.Connections.Use.team-pro"));
        Assert.Empty(_view.BindingFailures);
    }

    [Fact]
    public void FailedCheck_ShowsWhy_AndOffersToAddAnyway()
    {
        _probe.Failure = new System.Text.Json.JsonException("not a list");
        var services = new ConnectionServices(new ConnectionStore(_options, _settings, _secrets), _probe, new FakeDialogs(), _settings, _options, new StatusBarViewModel());
        _view.Show(() => new ConnectionsView { DataContext = new ConnectionsViewModel(services) });

        _view.Type("Agent.Connections.New.Endpoint", "http://localhost:8000/v1");
        _view.Press("Agent.Connections.New.Keyless");
        _view.Press("Agent.Connections.New.Add");

        Assert.Equal("Проверка не прошла. Ответ сервиса — не список моделей.", _view.TextOf("Agent.Connections.New.Status"));
        Assert.True(_view.IsShown("Agent.Connections.New.AddUnchecked"));

        _view.Press("Agent.Connections.New.AddUnchecked");

        Assert.True(_options.CurrentValue.Connections.Single().Keyless);
        Assert.True(_view.IsShown("Agent.Connections.Row.localhost"));
        Assert.Empty(_view.BindingFailures);
    }

    // Written values go through the real settings text and binder, as in the app.
    private void Reload()
    {
        var file = string.Empty;
        foreach (var (key, value) in _settings.Written)
        {
            if (value is not null)
            {
                file = SettingsJson.SetValue(file, key, value);
            }
        }

        var options = new AgentOptions();
        new ConfigurationBuilder().AddInMemoryCollection(SettingsJson.Parse(file)).Build().GetSection(AgentOptions.Section).Bind(options);
        _options.Set(options);
    }

    private sealed class Probe : IConnectionProbe
    {
        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<ServiceModel>> ListModelsAsync(AgentConnection connection, string? key, CancellationToken cancellationToken) =>
            Failure is null
                ? Task.FromResult<IReadOnlyList<ServiceModel>>([new ServiceModel("team-pro", 64_000), new ServiceModel("team-fast")])
                : Task.FromException<IReadOnlyList<ServiceModel>>(Failure);
    }

    private sealed class Secrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string? Get(string name) => _values.GetValueOrDefault(name);

        public void Set(string name, string value) => _values[name] = value;

        public void Remove(string name) => _values.Remove(name);
    }
}
