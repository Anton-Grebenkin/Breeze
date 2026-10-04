using CodeEditor.Shell.Instances;

namespace CodeEditor.Shell.Tests.Instances;

public sealed class ExplorerIntegrationTests
{
    private const string Launcher = @"C:\Users\me\AppData\Local\BreezeCodeEditor\Breeze.exe";

    private readonly IReadOnlyList<RegistryValue> _values = ExplorerIntegration.Values(Launcher, "Открыть в Breeze");

    [Theory]
    [InlineData(@"*\shell\Breeze", "%1")]
    [InlineData(@"Directory\shell\Breeze", "%V")]
    [InlineData(@"Directory\Background\shell\Breeze", "%V")]
    public void ContextMenu_OnFilesFoldersAndFolderBackground(string key, string argument)
    {
        Assert.Contains(new RegistryValue(key, null, "Открыть в Breeze"), _values);
        Assert.Contains(new RegistryValue(key, "Icon", $"\"{Launcher}\",0"), _values);
        Assert.Contains(new RegistryValue(key + @"\command", null, $"\"{Launcher}\" \"{argument}\""), _values);
    }

    [Fact]
    public void OpenWith_ListsBreezeForEveryFileType()
    {
        Assert.Contains(new RegistryValue(@"*\OpenWithList\Breeze.exe", null, string.Empty), _values);
        Assert.Contains(new RegistryValue(@"Applications\Breeze.exe\shell\open\command", null, $"\"{Launcher}\" \"%1\""), _values);
        Assert.Contains(new RegistryValue(@"Applications\Breeze.exe", "FriendlyAppName", "Breeze"), _values);
    }

    // Uninstall removes the listed keys with their subkeys: nothing registered may stay behind.
    [Fact]
    public void EveryValue_IsUnderAKeyRemovedOnUninstall() =>
        Assert.All(_values, value => Assert.Contains(ExplorerIntegration.Keys, key => value.Key == key || value.Key.StartsWith(key + @"\", StringComparison.Ordinal)));
}
