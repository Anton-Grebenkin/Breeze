using System.Runtime.Versioning;
using CodeEditor.Shell.Integration;
using Microsoft.Win32;

namespace CodeEditor.Shell.Tests.Integration;

/// <summary>Writes into a scratch key under <c>HKCU\Software</c> standing in for <c>HKEY_CURRENT_USER</c>.</summary>
[SupportedOSPlatform("windows")]
public sealed class ExplorerRegistryTests : IDisposable
{
    private const string ScratchParent = @"Software\BreezeTests";
    private const string Launcher = @"C:\Breeze\Breeze.exe";

    private readonly string _scratch = ScratchParent + @"\" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey _root;
    private readonly RegistrySet _contextMenu = ExplorerIntegration.ContextMenu(Launcher, "Open in Breeze");
    private readonly RegistrySet _fileTypes = FileTypes();

    public ExplorerRegistryTests() => _root = Registry.CurrentUser.CreateSubKey(_scratch);

    [Fact]
    public void Write_Twice_ChangesNothingTheSecondTime()
    {
        Assert.True(ExplorerRegistry.Write(_root, _contextMenu));

        Assert.False(ExplorerRegistry.Write(_root, _contextMenu));
        Assert.Equal($"\"{Launcher}\" \"%V\"", Read(@"Software\Classes\Directory\shell\Breeze\command"));
    }

    [Fact]
    public void Write_RestoresAChangedOrMissingValue()
    {
        ExplorerRegistry.Write(_root, _fileTypes);
        _root.DeleteSubKeyTree(@"Software\Classes\Breeze.cs");

        Assert.True(ExplorerRegistry.Write(_root, FileTypes("Файл {0}")));
        Assert.Equal($"\"{Launcher}\" \"%1\"", Read(@"Software\Classes\Breeze.cs\shell\open\command"));
        Assert.Equal("Файл JSON", Read(@"Software\Classes\Breeze.json"));
    }

    [Fact]
    public void Remove_LeavesOtherAppsAndSharedKeys()
    {
        using (var other = _root.CreateSubKey(@"Software\Classes\.cs\OpenWithProgids"))
        {
            other.SetValue("VSCode.cs", string.Empty);
        }

        ExplorerRegistry.Write(_root, _fileTypes);

        Assert.True(ExplorerRegistry.Remove(_root, _fileTypes));
        using var openWith = _root.OpenSubKey(@"Software\Classes\.cs\OpenWithProgids");
        Assert.Equal(["VSCode.cs"], openWith!.GetValueNames());
        Assert.All(_fileTypes.OwnedKeys, key => Assert.Null(_root.OpenSubKey(key)));
        using var registered = _root.OpenSubKey(@"Software\RegisteredApplications");
        Assert.Empty(registered!.GetValueNames());
    }

    // Removing what isn't there runs at every start with the option off; it must not make Explorer reread icons.
    [Fact]
    public void Remove_NothingRegistered_ReportsNoChange() =>
        Assert.False(ExplorerRegistry.Remove(_root, _fileTypes));

    [Fact]
    public void Remove_OneFeature_KeepsTheOther()
    {
        ExplorerRegistry.Write(_root, _contextMenu);
        ExplorerRegistry.Write(_root, _fileTypes);

        ExplorerRegistry.Remove(_root, _fileTypes);

        Assert.False(ExplorerRegistry.Write(_root, _contextMenu));
    }

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_scratch, throwOnMissingSubKey: false);
        using var parent = Registry.CurrentUser.OpenSubKey(ScratchParent, writable: true);
        if (parent is { SubKeyCount: 0 })
        {
            Registry.CurrentUser.DeleteSubKey(ScratchParent, throwOnMissingSubKey: false);
        }
    }

    private static RegistrySet FileTypes(string typeName = "{0} File") =>
        ExplorerIntegration.FileTypes(Launcher, typeName, "Code editor");

    private string? Read(string key)
    {
        using var opened = _root.OpenSubKey(key);
        return opened?.GetValue(null) as string;
    }
}
