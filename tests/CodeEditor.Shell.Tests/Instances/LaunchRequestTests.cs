using CodeEditor.Shell.Instances;

namespace CodeEditor.Shell.Tests.Instances;

public sealed class LaunchRequestTests
{
    [Fact]
    public void Empty_IsNoPathAndNoNewWindow() =>
        Assert.Equal(new LaunchRequest(null, NewWindow: false), LaunchRequest.Parse([]));

    [Theory]
    [InlineData("--new-window")]
    [InlineData("-n")]
    [InlineData("--NEW-WINDOW")]
    public void NewWindowFlag_AnyCase(string flag) =>
        Assert.True(LaunchRequest.Parse([flag]).NewWindow);

    [Fact]
    public void Path_BecomesFullPath_FlagAnywhere()
    {
        var request = LaunchRequest.Parse([@"C:\repo\..\src", "--new-window"]);

        Assert.Equal(new LaunchRequest(Path.GetFullPath(@"C:\src"), NewWindow: true), request);
    }
}
