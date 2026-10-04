using CodeEditor.Core.Storage;
using CodeEditor.Testing;

namespace CodeEditor.Core.Tests.Storage;

public sealed class DpapiSecretStoreTests
{
    private static readonly string UserData = Path.GetFullPath(@"C:\user");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(UserData);
    private readonly DpapiSecretStore _store;

    public DpapiSecretStoreTests() => _store = new DpapiSecretStore(_fileSystem, new UserDataPaths(UserData));

    [Fact]
    public void SetGetRemove_RoundTrip_FileIsEncrypted()
    {
        _store.Set("proxyapi", "sk-тестовый-ключ");

        Assert.Equal("sk-тестовый-ключ", _store.Get("proxyapi"));
        var bytes = _fileSystem.ReadAllBytes(Path.Combine(UserData, "secrets", "proxyapi.bin"));
        Assert.DoesNotContain("sk-", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);

        _store.Remove("proxyapi");
        Assert.Null(_store.Get("proxyapi"));
    }

    [Fact]
    public void Get_Missing_IsNull() => Assert.Null(_store.Get("none"));

    [Fact]
    public void Get_Corrupted_IsNull()
    {
        _fileSystem.AddBytes(Path.Combine(UserData, "secrets", "bad.bin"), [1, 2, 3]);

        Assert.Null(_store.Get("bad"));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a:b")]
    public void InvalidName_Throws(string name) => Assert.Throws<ArgumentException>(() => _store.Set(name, "x"));
}
