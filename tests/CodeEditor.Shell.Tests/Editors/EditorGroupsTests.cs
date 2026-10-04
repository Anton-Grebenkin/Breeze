using CodeEditor.Core.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Editors;

/// <summary>
/// Editor groups (ADR 0031): side by side, each with its own shown tab; files open in the active group; an emptied
/// group closes; reordering within a group; open to the side; split, move, focus and join commands.
/// </summary>
public sealed class EditorGroupsTests : IDisposable
{
    private readonly EditorAreaFixture _fixture = new();

    private EditorAreaViewModel Area => _fixture.Area;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task MoveToGroup_ShowsBothSideBySide_AndTheEmptyGroupCloses()
    {
        var a = await _fixture.OpenAsync("a.cs");
        var b = await _fixture.OpenAsync("b.cs");
        var right = Area.AddGroup(Area.ActiveGroup)!;

        Area.MoveToGroup(b, right);

        Assert.Equal([a], Area.Groups[0].Tabs);
        Assert.Equal((b, right, a), (Area.Active, Area.ActiveGroup, Area.Groups[0].Active));
        Assert.True(a.IsShown && b.IsShown);
        Assert.Equal(["EditorGroup.1", "EditorGroup.2"], Area.Groups.Select(group => group.AutomationId));

        Area.MoveToGroup(b, Area.Groups[0]);

        Assert.Single(Area.Groups);
        Assert.Equal(["a.cs", "b.cs"], _fixture.TabNames);
    }

    [Fact]
    public async Task Files_OpenInTheActiveGroup_ClosingItsLastTabReturnsToTheNeighbour()
    {
        await _fixture.OpenAsync("a.cs");
        var right = Area.AddGroup(Area.ActiveGroup)!;
        Area.Activate(right);

        var c = await _fixture.OpenAsync("c.cs");
        Assert.Same(right, Area.GroupOf(c));
        await Area.CloseAsync(c);

        Assert.Single(Area.Groups);
        Assert.Equal(("a.cs", true), (Area.Active?.Title, Area.Groups[0].IsActiveGroup));
    }

    [Fact]
    public async Task Reorder_WithinTheGroup()
    {
        await _fixture.OpenAsync("a.cs");
        await _fixture.OpenAsync("b.cs");
        var c = await _fixture.OpenAsync("c.cs");

        Area.MoveToGroup(c, Area.ActiveGroup, 0);
        Assert.Equal(["c.cs", "a.cs", "b.cs"], _fixture.TabNames);

        Area.MoveToGroup(c, Area.ActiveGroup, 3);
        Assert.Equal(["a.cs", "b.cs", "c.cs"], _fixture.TabNames);
    }

    // Diagram previews and git diffs open beside the file, in the second group.
    [Fact]
    public async Task ViewToTheSide_OpensInTheGroupOnTheRight()
    {
        await _fixture.OpenAsync("a.cs");

        var view = new EditorViews(Area).Open(new EditorViewRequest("preview", "Предпросмотр", () => new object()) { ToTheSide = true });

        Assert.Equal(2, Area.Groups.Count);
        Assert.Same(Area.Groups[1], Area.GroupOf(view));
        Assert.Equal("a.cs", Area.Groups[0].Active?.Title);
    }

    [Fact]
    public async Task Commands_SplitFocusMoveAndJoin()
    {
        var registry = new CommandRegistry();
        var service = new CommandService(registry, _fixture.Context, NullLogger<CommandService>.Instance);
        var shell = new ShellFixture();
        using var commands = new EditorGroupCommands(Area, service);
        commands.Register(registry, shell.Keybindings, shell.Menus);
        var a = await _fixture.OpenAsync("a.cs");
        var b = await _fixture.OpenAsync("b.cs");

        await Execute(service, EditorGroupCommands.SplitId);
        Assert.Equal(2, Area.Groups.Count);
        Assert.Same(Area.Groups[1], Area.GroupOf(b));

        await Execute(service, EditorGroupCommands.FocusGroupPrefix + "1");
        Assert.Same(a, Area.Active);

        await Execute(service, EditorGroupCommands.JoinGroupsId);
        Assert.Single(Area.Groups);
        Assert.Equal("Ctrl+\\", shell.Keybindings.FindForCommand(EditorGroupCommands.SplitId)?.Sequence.ToString());
    }

    private static async Task Execute(CommandService service, string id) =>
        await service.ExecuteAsync(id, cancellationToken: TestContext.Current.CancellationToken);
}
