using CodeEditor.Core.Context;
using CodeEditor.Modules.Docker.Commands;
using CodeEditor.Modules.Docker.Services.Model;
using CodeEditor.Modules.Docker.ViewModels;
using CodeEditor.Modules.Docker.ViewModels.Tree;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>
/// Panel refresh: docker does not run until the panel is shown; a visible panel refreshes periodically, a hidden one
/// does not; requests during a refresh merge. The selected node sets context keys for menus and keys.
/// </summary>
public sealed class DockerViewModelTests : IDisposable
{
    private readonly PanelFixture _fixture = new PanelFixture().WithTypicalState();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void HiddenPanel_RunsNothing()
    {
        Assert.Equal((DockerAvailability.Unknown, true, "Загрузка состояния Docker…"), (_fixture.Panel.Availability, _fixture.Panel.IsLoading, _fixture.Panel.Message));
        Assert.Empty(_fixture.Docker.Requests);
    }

    [Fact]
    public async Task VisiblePanel_RefreshesPeriodically_HiddenDoesNot()
    {
        await _fixture.ShowAsync();
        Assert.Equal((1, DockerAvailability.Available), (_fixture.Docker.Count("ps"), _fixture.Panel.Availability));

        _fixture.Time.Advance(DockerViewModel.RefreshInterval);
        await _fixture.Panel.RefreshAsync();
        var whileVisible = _fixture.Docker.Count("ps");

        await _fixture.Panel.SetVisible(false);
        _fixture.Time.Advance(DockerViewModel.RefreshInterval * 3);

        Assert.True(whileVisible >= 2, $"ps: {whileVisible}");
        Assert.Equal(whileVisible, _fixture.Docker.Count("ps"));
    }

    [Fact]
    public async Task RefreshDuringRefresh_RunsOnceMore()
    {
        var hold = _fixture.Docker.Hold("ps");
        var first = _fixture.Panel.RefreshAsync();
        var second = _fixture.Panel.RefreshAsync();
        var third = _fixture.Panel.RefreshAsync();

        hold.SetResult();
        await Task.WhenAll(first, second, third);

        Assert.Equal(2, _fixture.Docker.Count("ps"));
    }

    [Fact]
    public async Task OpeningAFolder_RefreshesTheVisiblePanel()
    {
        await _fixture.ShowAsync();

        _fixture.OpenFolderWithCompose();
        await _fixture.Panel.RefreshAsync();

        Assert.Single(_fixture.Panel.Tree.ComposeFiles);
    }

    [Fact]
    public async Task Selection_SetsContextKeysForMenusAndKeys()
    {
        await _fixture.ShowAsync();

        _fixture.Panel.Selected = _fixture.ContainerNode("app-api-1");
        Assert.True(Evaluate("dockerItem == container && dockerItemRunning && dockerItemHasPorts && dockerItemHasContainer"));

        _fixture.Panel.Selected = _fixture.ContainerNode("app-db-1");
        Assert.True(Evaluate("dockerItem == container && !dockerItemRunning && !dockerItemHasPorts"));

        _fixture.Panel.Selected = _fixture.Panel.Tree.AllImages.First();
        Assert.True(Evaluate("dockerItem == image && !dockerItemHasContainer"));

        _fixture.Panel.Selected = null;
        Assert.False(Evaluate("dockerItem == image"));
    }

    [Fact]
    public async Task ContextMenu_ShowsActionsForTheSelectedContainersState()
    {
        await _fixture.ShowAsync();

        _fixture.Panel.Selected = _fixture.ContainerNode("app-api-1");
        _fixture.Panel.PrepareContextMenu();
        var running = VisibleCommands();

        _fixture.Panel.Selected = _fixture.ContainerNode("app-db-1");
        _fixture.Panel.PrepareContextMenu();
        var stopped = VisibleCommands();

        Assert.Contains("Menu.docker.stop", running);
        Assert.Contains("Menu.docker.openInBrowser", running);
        Assert.DoesNotContain("Menu.docker.start", running);
        Assert.Contains("Menu.docker.start", stopped);
        Assert.DoesNotContain("Menu.docker.stop", stopped);
        Assert.Contains("Menu.docker.remove", stopped);
        Assert.DoesNotContain("Menu.docker.removeImage", stopped);
    }

    // Each node kind has its own menu: no group empties out, separators never double or sit at an edge.
    [Fact]
    public async Task ContextMenus_HaveNoDoubleOrTrailingSeparators()
    {
        _fixture.OpenFolderWithCompose(project: "other");
        await _fixture.ShowAsync();
        DockerNodeViewModel?[] nodes =
        [
            null,
            _fixture.Panel.Tree.Containers,
            _fixture.ContainerNode("app-api-1"),
            _fixture.ContainerNode("app-db-1"),
            _fixture.Panel.Tree.AllImages.First(),
            _fixture.Panel.Tree.ComposeFiles.First(),
            _fixture.Panel.Tree.Services.First(),
        ];

        foreach (var node in nodes)
        {
            _fixture.Panel.Selected = node;
            _fixture.Panel.PrepareContextMenu();
            var visible = _fixture.Panel.ContextMenu.Items.Where(item => item.IsVisible).Select(item => item.IsSeparator ? "|" : item.Header).ToList();

            Assert.False(visible[0] == "|" || visible[^1] == "|", string.Join(", ", visible));
            Assert.DoesNotContain("|, |", string.Join(", ", visible), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RemovedContainer_IsNoLongerSelected()
    {
        await _fixture.ShowAsync();
        _fixture.Panel.Selected = _fixture.ContainerNode("cache");

        _fixture.Docker.Answer("ps", PanelFixture.Container("a1", "app-api-1", "app-api", "running", "Up 2 hours", project: "app", service: "api"));
        await _fixture.Panel.RefreshAsync();

        Assert.Null(_fixture.Panel.Selected);
        Assert.False(Evaluate("dockerItemHasContainer"));
    }

    [Fact]
    public async Task CollapseAll_CollapsesGroupsAndFiles()
    {
        await _fixture.ShowAsync();

        _fixture.Panel.CollapseAllCommand.Execute(null);

        Assert.All(_fixture.Panel.Tree.Containers.Children, node => Assert.False(node.IsExpanded));
        Assert.True(_fixture.Panel.Tree.Containers.IsExpanded);
    }

    [Fact]
    public void ContextValues_MatchMenuConditions() =>
        Assert.Equal(["container", "image", "composeFile", "composeService"],
            new[] { DockerNodeKind.Container, DockerNodeKind.Image, DockerNodeKind.ComposeFile, DockerNodeKind.ComposeService }.Select(DockerMenus.ContextValue));

    private bool Evaluate(string expression) => _fixture.Context.Evaluate(ContextExpression.Parse(expression));

    private List<string> VisibleCommands() =>
        [.. _fixture.Panel.ContextMenu.Items.Where(item => !item.IsSeparator && item.IsVisible).Select(item => item.AutomationId)];
}
