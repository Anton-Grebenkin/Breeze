using System.Text.Json.Nodes;
using CodeEditor.Modules.Terminal.Commands;
using CodeEditor.Modules.Terminal.ViewModels;

namespace CodeEditor.Modules.Terminal.Tests.Panel;

public sealed class TerminalPageMessagesTests
{
    [Fact]
    public void Init_CarriesThemeAndFont()
    {
        var message = JsonNode.Parse(TerminalPageMessages.Init(new TerminalTheme("dark", "#1e1e1e", "#cccccc", "#2dd4bf", "rgba(38, 79, 120, 0.5)"), 14))!;

        Assert.Equal("init", (string?)message["type"]);
        Assert.Equal("dark", (string?)message["theme"]!["scheme"]);
        Assert.Equal("#1e1e1e", (string?)message["theme"]!["background"]);
        Assert.Equal(14, (double?)message["fontSize"]);
    }

    // Output carries escape sequences and quotes as they are.
    [Fact]
    public void Output_KeepsEscapesAndQuotes()
    {
        var message = JsonNode.Parse(TerminalPageMessages.Output(2, "\u001b[32m\"ok\"\u001b[0m\r\n"))!;

        Assert.Equal(2, (int?)message["id"]);
        Assert.Equal("\u001b[32m\"ok\"\u001b[0m\r\n", (string?)message["data"]);
    }

    [Fact]
    public void Read_InputAndResize()
    {
        Assert.Equal(new TerminalPageMessage("input", 1, "ls\r"), TerminalPageMessages.Read("""{"type":"input","id":1,"data":"ls\r"}"""));
        Assert.Equal(new TerminalPageMessage("resize", 1, Columns: 120, Rows: 30), TerminalPageMessages.Read("""{"type":"resize","id":1,"columns":120,"rows":30}"""));
        Assert.Equal(new TerminalPageMessage("copy", Data: "текст"), TerminalPageMessages.Read("""{"type":"copy","text":"текст"}"""));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{"id":1}""")]
    public void Read_Malformed_IsNull(string json) => Assert.Null(TerminalPageMessages.Read(json));

    [Theory]
    [InlineData("workbench.showCommands", true)]
    [InlineData("workbench.view.terminal", true)]
    [InlineData("workbench.view.explorer", true)]
    [InlineData("terminal.new", true)]
    [InlineData("workbench.action.closeActiveEditor", false)]
    [InlineData("editor.action.clipboardCopyAction", false)]
    public void TerminalKeys_KeepOnlyWorkbenchCommands(string command, bool keeps) =>
        Assert.Equal(keeps, TerminalKeys.KeepsKeys(command));
}
