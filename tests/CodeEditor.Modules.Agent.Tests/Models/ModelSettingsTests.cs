using System.Globalization;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Models;
using CodeEditor.Shell.Menus;

namespace CodeEditor.Modules.Agent.Tests.Models;

public sealed class ModelSettingsTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    private ModelSettingsViewModel Model => _fixture.Chat.Model;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Menu_ChecksCurrentValues()
    {
        _fixture.Options.Set(new AgentOptions { ReasoningEffort = AgentReasoningEffort.High, MaxOutputTokens = 8_192 });

        Assert.Equal(["Рассуждения", "Температура", "Длина ответа", "Окно контекста"], Model.ParameterMenu.Take(4).Select(item => item.Header));
        Assert.Equal("Высокий", Checked(Group("Рассуждения")).Header);
        Assert.Equal("По умолчанию", Checked(Group("Температура")).Header);
        Assert.Equal(8_192.ToString("N0", CultureInfo.CurrentCulture), Checked(Group("Длина ответа")).Header);
        Assert.Equal("Авто", Checked(Group("Окно контекста")).Header);
        Assert.Contains("рассуждения: высокий", Model.ParametersSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_ShowsValueSetByHand()
    {
        _fixture.Options.Set(new AgentOptions { Temperature = 0.3 });

        Assert.Equal(0.3.ToString("0.0#", CultureInfo.CurrentCulture), Checked(Group("Температура")).Header);
    }

    [Fact]
    public async Task Choice_WritesUserSetting_DefaultRemovesIt()
    {
        await Item("Рассуждения", "Максимальный").Command!.ExecuteAsync(null);
        await Item("Длина ответа", "По умолчанию").Command!.ExecuteAsync(null);

        Assert.Equal("extraHigh", _fixture.Settings.Written[ModelSettingsViewModel.ReasoningKey]);
        Assert.True(_fixture.Settings.Written.ContainsKey(ModelSettingsViewModel.MaxOutputTokensKey));
        Assert.Null(_fixture.Settings.Written[ModelSettingsViewModel.MaxOutputTokensKey]);
        Assert.Equal("Длина ответа: по умолчанию", _fixture.StatusBar.Message);
    }

    [Fact]
    public void Choice_WriteError_GoesToStatusBar()
    {
        _fixture.Settings.WriteError = "settings.json не разобран";

        Item("Рассуждения", "Низкий").Command!.Execute(null);

        Assert.Equal("settings.json не разобран", _fixture.StatusBar.Message);
    }

    // While prompts are tuned for the supported models, only they can be picked; no manager or free-form model input.
    [Fact]
    public async Task PickModel_ListsOnlySupportedModelsOfTheService()
    {
        _fixture.Options.Set(new AgentOptions());
        Model.PickModelCommand.Execute(null);

        var items = _fixture.QuickPick.Items;
        Assert.Equal(ModelCatalog.AitunnelModels, items.Select(item => item.Title));
        Assert.Equal("OpenAI · быстрая · текущая", items[0].Detail);
        Assert.Equal("xAI · оптимальная", items.Single(item => item.Title == "x-ai/grok-4.7").Detail);
        Assert.Equal("Anthropic · мощная", items.Single(item => item.Title == "anthropic/claude-opus-5.5").Detail);
        Assert.Equal("DeepSeek · быстрая", items.Single(item => item.Title == "deepseek/deepseek-v4.1-flash").Detail);
        Assert.Empty(_fixture.QuickPick.Shown!.Filter("ollama/qwen3-coder"));

        await _fixture.QuickPick.PickAsync("anthropic/claude-opus-5.5");

        Assert.Equal("anthropic/claude-opus-5.5", _fixture.Settings.Written[ModelSettingsViewModel.ModelKey]);
    }

    // The manager list (agent.models) is hidden with the manager; the current model stays listed even if not supported.
    [Fact]
    public void PickModel_IgnoresManagerList_KeepsCurrentModel()
    {
        _fixture.Options.Set(new AgentOptions { Model = "a/one", Models = ["a/one", "b/two"] });

        Model.PickModelCommand.Execute(null);

        Assert.Equal(["a/one", .. ModelCatalog.DefaultModels], _fixture.QuickPick.Items.Select(item => item.Title));
        Assert.Equal("one", Model.ModelShortName);
    }

    [Fact]
    public async Task PickHelperModel_SameAsAgentByDefault_AndWritesChoice()
    {
        Model.PickHelperModelCommand.Execute(null);
        Assert.Equal(("Как у агента", "текущая"), (_fixture.QuickPick.Items[0].Title, _fixture.QuickPick.Items[0].Detail));
        await _fixture.QuickPick.PickAsync("anthropic/claude-sonnet-5.5");
        Assert.Equal("anthropic/claude-sonnet-5.5", _fixture.Settings.Written[ModelSettingsViewModel.HelperModelKey]);

        _fixture.Options.Set(new AgentOptions { HelperModel = "anthropic/claude-sonnet-5.5" });
        Model.PickHelperModelCommand.Execute(null);
        await _fixture.QuickPick.PickAsync("Как у агента");

        Assert.Null(_fixture.Settings.Written[ModelSettingsViewModel.HelperModelKey]);
    }

    [Fact]
    public void ModelParametersCommand_OpensMenu()
    {
        Model.ShowParametersCommand.Execute(null);

        Assert.True(Model.IsParametersOpen);
    }

    // Roles (ADR 0012): the explorer defaults to low effort and the choice is saved. The advisor belongs to the hidden
    // Deep mode, so its model and effort aren't in the menu.
    [Fact]
    public async Task RolesMenu_ShowsRoleDefaults_AndWritesChoices()
    {
        var roles = Group("Роли");
        var explorer = roles.Items.Single(item => item.Header == "Рассуждения разведчика");

        Assert.Equal("Низкий", Checked(explorer).Header);
        Assert.DoesNotContain(roles.Items, item => item.Header is "Модель советника…" or "Рассуждения советника");
        Assert.Contains(roles.Items, item => item.Header == "Служебная модель…");

        await explorer.Items.Single(item => item.Header == "Максимальный").Command!.ExecuteAsync(null);

        Assert.Equal("extraHigh", _fixture.Settings.Written[ModelSettingsViewModel.ExplorerReasoningKey]);
    }

    [Fact]
    public async Task PickAdvisorModel_WritesChoice()
    {
        Model.PickAdvisorModelCommand.Execute(null);
        await _fixture.QuickPick.PickAsync("anthropic/claude-opus-5.5");

        Assert.Equal("anthropic/claude-opus-5.5", _fixture.Settings.Written[ModelSettingsViewModel.AdvisorModelKey]);
    }

    private MenuItemViewModel Group(string header) => Model.ParameterMenu.Single(item => item.Header == header);

    private MenuItemViewModel Item(string group, string header) => Group(group).Items.Single(item => item.Header == header);

    private static MenuItemViewModel Checked(MenuItemViewModel group) => Assert.Single(group.Items, item => item.IsChecked);
}
