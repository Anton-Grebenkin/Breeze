using CodeEditor.Core.Storage;

namespace CodeEditor.Core.Tests.Storage;

/// <summary>User data folder: the former <c>CodeEditor</c> folder moves to <c>Breeze</c> (ADR 0038).</summary>
public sealed class UserDataPathsTests : IDisposable
{
    private readonly string _localAppData = Path.Combine(Path.GetTempPath(), "CodeEditor.Tests", "userdata-" + Guid.NewGuid().ToString("N"));

    public UserDataPathsTests() => Directory.CreateDirectory(_localAppData);

    public void Dispose() => Directory.Delete(_localAppData, recursive: true);

    [Fact]
    public void DefaultRoot_MovesLegacyFolderWithData()
    {
        var legacy = Path.Combine(_localAppData, "CodeEditor");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "{}");

        var root = UserDataPaths.DefaultRoot(_localAppData);

        Assert.Equal(Path.Combine(_localAppData, "Breeze"), root);
        Assert.True(File.Exists(Path.Combine(root, "settings.json")));
        Assert.False(Directory.Exists(legacy));
    }

    [Fact]
    public void DefaultRoot_PrefersExistingFolder_AndLeavesLegacyAlone()
    {
        Directory.CreateDirectory(Path.Combine(_localAppData, "Breeze"));
        Directory.CreateDirectory(Path.Combine(_localAppData, "CodeEditor"));

        var root = UserDataPaths.DefaultRoot(_localAppData);

        Assert.Equal(Path.Combine(_localAppData, "Breeze"), root);
        Assert.True(Directory.Exists(Path.Combine(_localAppData, "CodeEditor")));
    }

    [Fact]
    public void DefaultRoot_WithoutLegacyFolder_ReturnsNewPathWithoutCreatingIt()
    {
        var root = UserDataPaths.DefaultRoot(_localAppData);

        Assert.Equal(Path.Combine(_localAppData, "Breeze"), root);
        Assert.False(Directory.Exists(root));
    }
}
