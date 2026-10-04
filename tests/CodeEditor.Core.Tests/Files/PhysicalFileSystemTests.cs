using CodeEditor.Core.Files;

namespace CodeEditor.Core.Tests.Files;

/// <summary>
/// File operations on a real temp folder.
/// </summary>
public sealed class PhysicalFileSystemTests : IDisposable
{
    private readonly PhysicalFileSystem _fileSystem = new();

    private readonly string _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "CodeEditor.Tests", Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task CopyFile_CopiesContentAndKeepsSource()
    {
        var source = Path.Combine(_root, "a.txt");
        var destination = Path.Combine(_root, "b.txt");
        await File.WriteAllTextAsync(source, "текст", TestContext.Current.CancellationToken);

        _fileSystem.CopyFile(source, destination);

        Assert.Equal("текст", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task CopyFile_OntoExistingPath_Throws()
    {
        var source = Path.Combine(_root, "a.txt");
        var destination = Path.Combine(_root, "b.txt");
        await File.WriteAllTextAsync(source, "a", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(destination, "b", TestContext.Current.CancellationToken);

        Assert.Throws<IOException>(() => _fileSystem.CopyFile(source, destination));
        Assert.Equal("b", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
    }
}
