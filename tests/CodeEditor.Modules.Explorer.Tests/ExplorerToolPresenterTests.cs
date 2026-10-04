using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Explorer.Services;

namespace CodeEditor.Modules.Explorer.Tests;

public sealed class ExplorerToolPresenterTests
{
    private readonly ExplorerToolPresenter _presenter = new();

    [Fact]
    public void ReadFile_RunningThenDone_WithLineRange()
    {
        var arguments = new Dictionary<string, object?> { ["path"] = "src/Shop/Order.cs" };

        var running = _presenter.Present(new AgentToolCall(ExplorerAgentTools.ReadFileName, arguments))!;
        var done = _presenter.Present(new AgentToolCall(ExplorerAgentTools.ReadFileName, arguments, "1\tusing System;\n2\t\n80\t}\n…(строки 1–80 из 120; продолжение — startLine=81)"))!;

        Assert.Equal(("Чтение Order.cs", AgentToolIcon.Read, true), (running.Title, running.Icon, running.IsExploration));
        Assert.Null(running.Detail);
        Assert.Equal(("Прочитан Order.cs", "строки 1–80", "src/Shop/Order.cs"), (done.Title, done.Detail, done.FilePath));
    }

    [Theory]
    [InlineData("(пустой файл)", "пустой")]
    [InlineData("(Строки 1–10 файла a.cs не изменились с прошлого чтения в этом чате — используйте прежний результат.)", "без изменений")]
    public void ReadFile_SpecialResults(string result, string detail) =>
        Assert.Equal(detail, _presenter.Present(new AgentToolCall(ExplorerAgentTools.ReadFileName, new Dictionary<string, object?> { ["path"] = "a.cs" }, result))!.Detail);

    [Fact]
    public void ListDirAndFindFiles_CountEntries()
    {
        var list = _presenter.Present(new AgentToolCall(ExplorerAgentTools.ListDirName, new Dictionary<string, object?> { ["path"] = "src" }, "a/\nb.cs\nc.cs"))!;
        var find = _presenter.Present(new AgentToolCall(ExplorerAgentTools.FindFilesName, new Dictionary<string, object?> { ["pattern"] = "*.cs" }, "a.cs\n…(ещё 3; уточните маску)"))!;

        Assert.Equal(("Просмотрена папка src", "3 элемента"), (list.Title, list.Detail));
        Assert.Equal(("Найдены файлы «*.cs»", "1 файл"), (find.Title, find.Detail));
    }

    [Fact]
    public void DeleteAndMove_AreNotExploration()
    {
        var delete = _presenter.Present(new AgentToolCall(ExplorerEditingTools.DeleteFileName, new Dictionary<string, object?> { ["path"] = "old/a.txt" }, "ok"))!;
        var move = _presenter.Present(new AgentToolCall(ExplorerEditingTools.MoveFileName, new Dictionary<string, object?> { ["from"] = "a.cs", ["to"] = "src/b.cs" }, "ok"))!;

        Assert.Equal(("Удалён a.txt", false), (delete.Title, delete.IsExploration));
        Assert.Equal(("Перенесён a.cs → src/b.cs", "src/b.cs"), (move.Title, move.FilePath));
    }

    [Fact]
    public void OtherTools_AreNotDescribed() =>
        Assert.Null(_presenter.Present(new AgentToolCall("search_text", new Dictionary<string, object?>())));
}
