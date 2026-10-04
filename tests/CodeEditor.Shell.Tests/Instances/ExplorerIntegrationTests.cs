using CodeEditor.Shell.Instances;

namespace CodeEditor.Shell.Tests.Instances;

public sealed class ExplorerIntegrationTests
{
    private const string Launcher = @"C:\Users\me\AppData\Local\BreezeCodeEditor\Breeze.exe";
    private const string OpenFile = $"\"{Launcher}\" \"%1\"";
    private const string Icon = $"\"{Launcher}\",0";

    private readonly IReadOnlyList<RegistryValue> _values =
        ExplorerIntegration.Values(Launcher, new ExplorerTexts("Открыть в Breeze", "Файл {0}", "Редактор кода"));

    [Theory]
    [InlineData(@"Software\Classes\*\shell\Breeze", "%1")]
    [InlineData(@"Software\Classes\Directory\shell\Breeze", "%V")]
    [InlineData(@"Software\Classes\Directory\Background\shell\Breeze", "%V")]
    public void ContextMenu_OnFilesFoldersAndFolderBackground(string key, string argument)
    {
        Assert.Contains(new RegistryValue(key, null, "Открыть в Breeze"), _values);
        Assert.Contains(new RegistryValue(key, "Icon", Icon), _values);
        Assert.Contains(new RegistryValue(key + @"\command", null, $"\"{Launcher}\" \"{argument}\""), _values);
    }

    [Fact]
    public void OpenWith_ListsBreezeForEveryFileType()
    {
        Assert.Contains(new RegistryValue(@"Software\Classes\*\OpenWithList\Breeze.exe", null, string.Empty), _values);
        Assert.Contains(new RegistryValue(@"Software\Classes\Applications\Breeze.exe\shell\open\command", null, OpenFile), _values);
        Assert.Contains(new RegistryValue(@"Software\Classes\Applications\Breeze.exe", "FriendlyAppName", "Breeze"), _values);
    }

    // The "Open with" submenu and "Default apps" take apps from the extension's OpenWithProgids and the app's capabilities.
    [Fact]
    public void FileType_HasItsOwnProgIdOfferedForTheExtension()
    {
        Assert.Contains(new RegistryValue(@"Software\Classes\Breeze.cs", null, "Файл CS"), _values);
        Assert.Contains(new RegistryValue(@"Software\Classes\Breeze.cs\DefaultIcon", null, Icon), _values);
        Assert.Contains(new RegistryValue(@"Software\Classes\Breeze.cs\shell\open\command", null, OpenFile), _values);
        Assert.Contains(new RegistryValue(@"Software\Classes\.cs\OpenWithProgids", "Breeze.cs", string.Empty), _values);
        Assert.Contains(new RegistryValue(@"Software\Classes\Applications\Breeze.exe\SupportedTypes", ".cs", string.Empty), _values);
        Assert.Contains(new RegistryValue(@"Software\BreezeCodeEditor\Capabilities\FileAssociations", ".cs", "Breeze.cs"), _values);
    }

    [Fact]
    public void DefaultApps_ListBreezeWithItsCapabilities()
    {
        Assert.Contains(new RegistryValue(@"Software\RegisteredApplications", "Breeze", @"Software\BreezeCodeEditor\Capabilities"), _values);
        Assert.Contains(new RegistryValue(@"Software\BreezeCodeEditor\Capabilities", "ApplicationName", "Breeze"), _values);
        Assert.Contains(new RegistryValue(@"Software\BreezeCodeEditor\Capabilities", "ApplicationDescription", "Редактор кода"), _values);
    }

    // Uninstall deletes owned keys whole and only Breeze's named values elsewhere: .cs or RegisteredApplications stay.
    [Fact]
    public void Values_OutsideOwnedKeys_AreNamedValuesInSharedKeys()
    {
        var shared = _values.Where(value => !ExplorerIntegration.IsOwned(value.Key)).ToList();

        Assert.Equal(OpenWithFileTypes.Extensions.Length + 1, shared.Count);
        Assert.All(shared, value => Assert.NotNull(value.Name));
        Assert.All(shared, value => Assert.True(
            value.Key == @"Software\RegisteredApplications" || value.Key.EndsWith(@"\OpenWithProgids", StringComparison.Ordinal),
            value.Key));
    }

    [Theory]
    [InlineData(@"Software\Classes\.cs")]
    [InlineData(@"Software\Classes\*")]
    [InlineData(@"Software\Classes\Directory")]
    [InlineData(@"Software\Classes\Applications")]
    [InlineData(@"Software\Classes")]
    public void SharedKeys_AreNotOwned(string key) => Assert.False(ExplorerIntegration.IsOwned(key));

    [Fact]
    public void OwnedKeys_IncludeSubkeys_IgnoringCase() =>
        Assert.True(ExplorerIntegration.IsOwned(@"software\classes\breeze.CS\shell\open"));
}
