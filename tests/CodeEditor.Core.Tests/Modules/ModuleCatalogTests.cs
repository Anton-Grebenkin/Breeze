using CodeEditor.Core.Modules;

namespace CodeEditor.Core.Tests.Modules;

public sealed class ModuleCatalogTests
{
    [Fact]
    public void Create_OrdersDependenciesFirst()
    {
        var catalog = ModuleCatalog.Create(
        [
            new TestModule("agent", ["explorer", "editor"]),
            new TestModule("editor", ["explorer"]),
            new TestModule("explorer"),
        ]);

        Assert.Equal(["explorer", "editor", "agent"], Ids(catalog));
        Assert.Empty(catalog.Rejected);
    }

    [Fact]
    public void Create_KeepsInputOrderForIndependentModules()
    {
        var catalog = ModuleCatalog.Create([new TestModule("c"), new TestModule("a"), new TestModule("b")]);

        Assert.Equal(["c", "a", "b"], Ids(catalog));
    }

    [Fact]
    public void Create_DuplicateId_KeepsFirst()
    {
        var first = new TestModule("explorer");

        var catalog = ModuleCatalog.Create([first, new TestModule("explorer")]);

        Assert.Same(first, Assert.Single(catalog.Modules));
        Assert.Equal(new ModuleRejection("explorer", ModuleRejectionReason.DuplicateId), Assert.Single(catalog.Rejected));
    }

    [Fact]
    public void Create_MissingDependency_RejectsModuleAndDependents()
    {
        var catalog = ModuleCatalog.Create(
        [
            new TestModule("explorer"),
            new TestModule("git", ["terminal"]),
            new TestModule("agent", ["git"]),
        ]);

        Assert.Equal(["explorer"], Ids(catalog));
        Assert.Contains(new ModuleRejection("git", ModuleRejectionReason.MissingDependency, "terminal"), catalog.Rejected);
        Assert.Contains(new ModuleRejection("agent", ModuleRejectionReason.DependencyRejected, "git"), catalog.Rejected);
    }

    [Fact]
    public void Create_Cycle_RejectsCycleMembersAndDependents()
    {
        var catalog = ModuleCatalog.Create(
        [
            new TestModule("a", ["b"]),
            new TestModule("b", ["c"]),
            new TestModule("c", ["a"]),
            new TestModule("d", ["a"]),
            new TestModule("e"),
        ]);

        Assert.Equal(["e"], Ids(catalog));
        Assert.Equal(
            [
                ModuleRejectionReason.CircularDependency,
                ModuleRejectionReason.CircularDependency,
                ModuleRejectionReason.CircularDependency,
                ModuleRejectionReason.DependencyRejected,
            ],
            catalog.Rejected.Select(rejection => rejection.Reason));
    }

    [Fact]
    public void Create_SelfDependency_IsCycle()
    {
        var catalog = ModuleCatalog.Create([new TestModule("a", ["a"])]);

        Assert.Equal(ModuleRejectionReason.CircularDependency, Assert.Single(catalog.Rejected).Reason);
    }

    [Fact]
    public void Create_RepeatedDependency_CountsOnce()
    {
        var catalog = ModuleCatalog.Create([new TestModule("b", ["a", "a"]), new TestModule("a")]);

        Assert.Equal(["a", "b"], Ids(catalog));
    }

    private static IEnumerable<string> Ids(ModuleCatalog catalog) => catalog.Modules.Select(module => module.Info.Id);
}
