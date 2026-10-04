using CodeEditor.Core.Commands;

namespace CodeEditor.Core.Tests.Commands;

public sealed class CommandRegistryTests
{
    private readonly CommandRegistry _registry = new();

    [Fact]
    public void Register_MakesCommandAvailable()
    {
        var command = CreateCommand("file.save");

        _registry.Register(command);

        Assert.True(_registry.TryGet("file.save", out var found));
        Assert.Same(command, found);
        Assert.Equal([command], _registry.Commands);
    }

    [Fact]
    public void Register_DuplicateId_Throws()
    {
        _registry.Register(CreateCommand("file.save"));

        Assert.Throws<InvalidOperationException>(() => _registry.Register(CreateCommand("file.save")));
    }

    [Fact]
    public void Dispose_RemovesCommandAndIsIdempotent()
    {
        var registration = _registry.Register(CreateCommand("file.save"));

        registration.Dispose();
        registration.Dispose();

        Assert.False(_registry.TryGet("file.save", out _));
        Assert.Empty(_registry.Commands);
    }

    [Fact]
    public void Dispose_OldRegistration_DoesNotRemoveNewCommandWithSameId()
    {
        var old = _registry.Register(CreateCommand("file.save"));
        old.Dispose();
        var replacement = CreateCommand("file.save");
        _registry.Register(replacement);

        old.Dispose();

        Assert.True(_registry.TryGet("file.save", out var found));
        Assert.Same(replacement, found);
    }

    [Fact]
    public void Commands_SnapshotIsReusedUntilChange()
    {
        _registry.Register(CreateCommand("a"));
        var first = _registry.Commands;

        Assert.Equal(first, _registry.Commands);

        _registry.Register(CreateCommand("b"));

        Assert.Equal(2, _registry.Commands.Length);
    }

    [Fact]
    public void Changed_RaisedOnRegisterAndUnregister()
    {
        var changes = 0;
        _registry.Changed += (_, _) => changes++;

        _registry.Register(CreateCommand("a")).Dispose();

        Assert.Equal(2, changes);
    }

    [Fact]
    public void DisplayTitle_IncludesCategory()
    {
        Assert.Equal("Файл: Сохранить", CreateCommand("file.save", "Файл").DisplayTitle);
        Assert.Equal("Сохранить", CreateCommand("file.save").DisplayTitle);
    }

    private static CommandDefinition CreateCommand(string id, string? category = null) =>
        new(id, "Сохранить", (_, _) => ValueTask.CompletedTask, category);
}
