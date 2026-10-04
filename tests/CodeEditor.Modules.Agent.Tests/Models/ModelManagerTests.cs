using System.ClientModel;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Models;

namespace CodeEditor.Modules.Agent.Tests.Models;

/// <summary>Model manager: service models by vendor, toggles writing agent.models, search and custom models.</summary>
public sealed class ModelManagerTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private readonly ModelManagerViewModel _manager;

    public ModelManagerTests()
    {
        _fixture.Options.Set(new AgentOptions { Model = "openai/gpt-6-luna", Models = ["openai/gpt-6-luna", "x-ai/grok-4.7"] });
        _fixture.ModelList.Models = ["openai/gpt-6-luna", "openai/gpt-6-sol", "x-ai/grok-4.7", "qwen/qwen3-coder-plus", "recraft/recraft-v3", "openai/gpt-5-mini-2025-08-07", "openai/gpt-5-mini"];
        _manager = _fixture.CreateModelManager();
    }

    public void Dispose()
    {
        _manager.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task Open_GroupsChatModelsByVendor_PopularFirst_EnabledGroupsExpanded()
    {
        await _manager.OpenCommand.ExecuteAsync(null);

        var vendors = _manager.Rows.Where(row => row.IsVendor).Select(row => row.Title).ToList();
        Assert.Equal(["OpenAI", "xAI", "Qwen"], vendors);
        Assert.DoesNotContain(_manager.Rows, row => row.Id.StartsWith("recraft/", StringComparison.Ordinal) || row.Id.EndsWith("-08-07", StringComparison.Ordinal));
        Assert.Contains(_manager.Rows, row => row is { Id: "x-ai/grok-4.7", IsChecked: true });
        Assert.DoesNotContain(_manager.Rows, row => row.Id == "qwen/qwen3-coder-plus");
        Assert.True(_manager.Rows.Single(row => row.Id == "openai/gpt-6-luna").IsCurrent);
        Assert.Equal("Моделей: 5, включено: 2", _manager.Status);
    }

    [Fact]
    public async Task Toggle_WritesModelsInVendorOrder_AndPickerShowsThem()
    {
        await _manager.OpenCommand.ExecuteAsync(null);

        _manager.Rows.Single(row => row.Id == "openai/gpt-6-sol").IsChecked = true;
        _manager.Rows.Single(row => row.Id == "x-ai/grok-4.7").IsChecked = false;
        Assert.Equal("0 из 1", _manager.Rows.Single(row => row is { IsVendor: true, Id: "x-ai" }).Detail);

        Assert.Equal(new[] { "openai/gpt-6-luna", "openai/gpt-6-sol" }, _fixture.Settings.Written[ModelManagerViewModel.ModelsKey]);
        Assert.Equal(["openai/gpt-6-luna", "openai/gpt-6-sol"], ModelCatalog.ModelsFor(_fixture.Options.CurrentValue));
    }

    [Fact]
    public async Task Search_ExpandsMatchingGroups_CustomModelCanBeAdded()
    {
        await _manager.OpenCommand.ExecuteAsync(null);

        _manager.Search = "coder";
        Assert.Equal(["qwen", "qwen/qwen3-coder-plus"], _manager.Rows.Select(row => row.Id));
        Assert.False(_manager.AddCustomCommand.CanExecute(null));

        _manager.Search = "z-ai/glm-5.3";
        Assert.True(_manager.AddCustomCommand.CanExecute(null));
        _manager.AddCustomCommand.Execute(null);

        Assert.Contains("z-ai/glm-5.3", ModelCatalog.ModelsFor(_fixture.Options.CurrentValue));
        Assert.Contains(_manager.Rows, row => row is { Id: "z-ai/glm-5.3", IsChecked: true });
    }

    // "Supported only" (ADR 0017): the enabled models are exactly the supported ones.
    [Fact]
    public async Task UseSupported_KeepsOnlySupportedModels()
    {
        await _manager.OpenCommand.ExecuteAsync(null);

        _manager.UseSupportedCommand.Execute(null);

        var written = Assert.IsType<string[]>(_fixture.Settings.Written[ModelManagerViewModel.ModelsKey]);
        Assert.Equal(ModelCatalog.DefaultModels.Order(), written.Order());
        Assert.DoesNotContain("qwen/qwen3-coder-plus", ModelCatalog.ModelsFor(_fixture.Options.CurrentValue));
    }

    [Fact]
    public async Task ServiceError_KeepsCachedList_AndSaysWhy()
    {
        await _manager.OpenCommand.ExecuteAsync(null);
        _fixture.ModelList.Failure = new ClientResultException("Unauthorized");

        await _manager.RefreshCommand.ExecuteAsync(null);

        Assert.StartsWith("Не удалось загрузить список моделей: Unauthorized", _manager.Status, StringComparison.Ordinal);
        Assert.Contains(_manager.Rows, row => row.Id == "openai/gpt-6-sol");
    }

    [Fact]
    public async Task NextOpen_ShowsCachedListBeforeNetwork()
    {
        await _manager.OpenCommand.ExecuteAsync(null);
        _fixture.ModelList.Failure = new ClientResultException("offline");
        using var reopened = _fixture.CreateModelManager();

        await reopened.OpenCommand.ExecuteAsync(null);

        Assert.Contains(reopened.Rows, row => row.Id == "openai/gpt-6-sol");
    }

    [Theory]
    [InlineData("openai/gpt-3.5-turbo-0125", false)]
    [InlineData("qwen/qwen3-14b-04-28", false)]
    [InlineData("mistralai/devstral-2512", true)]
    [InlineData("openai/gpt-image-1", false)]
    [InlineData("voyageai/voyage-3", false)]
    public void Filter_DropsMediaModels_AndSnapshotsOfUndatedModels(string model, bool kept)
    {
        var models = ChatModelFilter.Chat([model, "openai/gpt-3.5-turbo", "qwen/qwen3-14b"]);

        Assert.Equal(kept, models.Contains(model));
    }
}
