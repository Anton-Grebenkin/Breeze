using CodeEditor.Modules.Docker.ViewModels.Tree;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>
/// The panel tree: containers by compose project, images, compose files with services. A refresh updates existing
/// nodes, so selection and expansion stay and rows do not jump; a node with a running action shows it.
/// </summary>
public sealed class DockerTreeTests : IDisposable
{
    private readonly PanelFixture _fixture = new PanelFixture().WithTypicalState();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Containers_AreGroupedByComposeProject_ThenStandalone()
    {
        await _fixture.ShowAsync();

        var section = _fixture.Panel.Tree.Containers;
        var group = Assert.IsType<ContainerGroupViewModel>(section.Children[0]);
        Assert.Equal(["app", "cache"], section.Children.Select(node => node.Title));
        Assert.Equal(["app-api-1", "app-db-1"], group.Children.Select(node => node.Title));
        Assert.Equal(("1 из 2 работает", true, DockerTone.Running), (group.Description, group.IsExpanded, group.Tone));
        Assert.Equal("2 из 3 работают", section.Description);

        var api = _fixture.ContainerNode("app-api-1");
        Assert.Equal(("работает 2 ч · app-api", DockerIcons.Running, DockerTone.Running), (api.Description, api.Icon, api.Tone));
        Assert.Equal(":8080", Assert.Single(api.Links).Label);
        Assert.Equal(("остановлен 3 дн. назад · postgres:17", DockerTone.Muted), (_fixture.ContainerNode("app-db-1").Description, _fixture.ContainerNode("app-db-1").Tone));
    }

    [Fact]
    public async Task Refresh_KeepsNodesAndSelection_AndUpdatesState()
    {
        await _fixture.ShowAsync();
        var cache = _fixture.ContainerNode("cache");
        cache.IsSelected = true;
        _fixture.Panel.Selected = cache;

        _fixture.Docker.Answer("ps", string.Join('\n',
            PanelFixture.Container("a1", "app-api-1", "app-api", "running", "Up 2 hours", "0.0.0.0:8080->80/tcp", "app", "api"),
            PanelFixture.Container("c1", "cache", "redis:7", "exited", "Exited (0) 1 second ago"),
            PanelFixture.Container("b1", "broker", "rabbitmq:3", "running", "Up 1 second")));
        await _fixture.Panel.RefreshAsync();

        Assert.Same(cache, _fixture.ContainerNode("cache"));
        Assert.True(cache.IsSelected);
        Assert.Equal("остановлен 1 с назад · redis:7", cache.Description);
        Assert.Empty(cache.Links);
        Assert.Equal(["app", "broker", "cache"], _fixture.Panel.Tree.Containers.Children.Select(node => node.Title));
        Assert.DoesNotContain(_fixture.Panel.Tree.AllContainers, node => node.Title == "app-db-1");
    }

    [Fact]
    public async Task BusyText_ShowsInsteadOfTheState_AndSurvivesRefresh()
    {
        await _fixture.ShowAsync();
        var api = _fixture.ContainerNode("app-api-1");

        api.BusyText = "останавливается…";
        await _fixture.Panel.RefreshAsync();

        Assert.Equal(("останавливается…", true), (api.Detail, api.IsBusy));
        api.BusyText = null;
        Assert.Equal(api.Description, api.Detail);
    }

    [Fact]
    public async Task Images_NamedFirst_DanglingLastAndMuted()
    {
        await _fixture.ShowAsync();

        var images = _fixture.Panel.Tree.AllImages.ToList();
        Assert.Equal(["postgres:17", "<none>"], images.Select(node => node.Title));
        Assert.StartsWith("451 МБ · ", images[0].Description, StringComparison.Ordinal);
        Assert.Equal(DockerTone.Muted, images[1].Tone);
    }

    [Fact]
    public async Task ComposeServices_ShowTheirContainers()
    {
        _fixture.OpenFolderWithCompose();

        await _fixture.ShowAsync();

        var file = Assert.Single(_fixture.Panel.Tree.ComposeFiles);
        Assert.Equal(("compose.yaml", "проект app · 1 из 2 работает", true), (file.Title, file.Description, file.IsExpanded));
        var services = _fixture.Panel.Tree.Services.ToList();
        Assert.Equal(["api", "db"], services.Select(service => service.Title));
        Assert.Equal(("a1", ":8080"), (services[0].Container?.Id, Assert.Single(services[0].Links).Label));
        Assert.Equal("остановлен 3 дн. назад", services[1].Description);
    }

    [Fact]
    public async Task ServiceWithoutContainer_IsNotStarted()
    {
        _fixture.OpenFolderWithCompose(project: "other");

        await _fixture.ShowAsync();

        var file = Assert.Single(_fixture.Panel.Tree.ComposeFiles);
        Assert.Equal("проект other", file.Description);
        Assert.All(_fixture.Panel.Tree.Services, service => Assert.Equal(("не запущена", DockerTone.Muted), (service.Description, service.Tone)));
    }

    [Fact]
    public async Task UnreadableComposeFile_ShowsTheError()
    {
        _fixture.OpenFolderWithCompose();
        _fixture.Docker.Answer("compose config", "yaml: line 3: mapping values are not allowed in this context", exitCode: 1);

        await _fixture.ShowAsync();

        var file = Assert.Single(_fixture.Panel.Tree.ComposeFiles);
        Assert.Equal(("не прочитан: yaml: line 3: mapping values are not allowed in this context", DockerTone.Error), (file.Description, file.Tone));
        Assert.Empty(file.Children);
    }

    [Fact]
    public async Task EmptyDocker_ShowsHints()
    {
        _fixture.Docker.Answer("ps", string.Empty).Answer("images", string.Empty);
        _fixture.Workspace.Open(PanelFixture.Root);

        await _fixture.ShowAsync();

        Assert.Equal(["Контейнеров нет"], _fixture.Panel.Tree.Containers.Children.Select(node => node.Title));
        Assert.Equal(["Образов нет"], _fixture.Panel.Tree.Images.Children.Select(node => node.Title));
        Assert.Equal(["В папке нет файлов compose"], _fixture.Panel.Tree.Compose.Children.Select(node => node.Title));
        Assert.Equal(string.Empty, _fixture.Panel.Tree.Containers.Description);
    }
}
