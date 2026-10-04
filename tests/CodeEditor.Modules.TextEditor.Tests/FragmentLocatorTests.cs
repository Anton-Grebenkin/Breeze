using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.TextEditor.Services.Agent;

namespace CodeEditor.Modules.TextEditor.Tests;

public sealed class FragmentLocatorTests
{
    private const string Code = "class A\n{\n    void Run()\n    {\n        Say(\"hi\");   \n    }\n}\n";

    [Fact]
    public void Exact_IsFoundWithoutWarning()
    {
        var found = FragmentLocator.Locate(Code, "Say(\"hi\");", "Say(\"bye\");", "A.cs");

        Assert.Equal(Code.IndexOf("Say", StringComparison.Ordinal), found.Offset);
        Assert.Null(found.Warning);
    }

    [Fact]
    public void TypographicQuotes_MatchStraightOnes()
    {
        var found = FragmentLocator.Locate(Code, "Say(“hi”);", "Say(“bye”);", "A.cs");

        Assert.Equal("Say(\"bye\");", found.NewText);
        Assert.Null(found.Warning);
    }

    [Fact]
    public void TrailingSpaces_AreIgnored()
    {
        var found = FragmentLocator.Locate(Code, "        Say(\"hi\");\n    }", "        Say(\"bye\");\n    }", "A.cs");

        Assert.Equal("        Say(\"hi\");   \n    }", Code.Substring(found.Offset, found.Length));
        Assert.Null(found.Warning);
    }

    [Fact]
    public void WrongIndent_IsMatchedAndReindented_WithWarning()
    {
        var found = FragmentLocator.Locate(Code, "void Run()\n{\n    Say(\"hi\");", "void Run()\n{\n    Say(\"bye\");", "A.cs");

        Assert.Equal("    void Run()\n    {\n        Say(\"hi\");   ", Code.Substring(found.Offset, found.Length));
        Assert.Equal("    void Run()\n    {\n        Say(\"bye\");", found.NewText);
        Assert.Contains("без учёта отступов (строки 3–5)", found.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public void TabsFromModel_BecomeFileSpaces_AtEveryLevel()
    {
        // The model read "3\t    void Run()" and kept the line-number tab instead of the indent.
        var found = FragmentLocator.Locate(Code, "\tvoid Run()\n\t{\n\t\tSay(\"hi\");", "\tvoid Run()\n\t{\n\t\tif (ok)\n\t\t\tSay(\"bye\");", "A.cs");

        Assert.Equal("    void Run()\n    {\n        if (ok)\n            Say(\"bye\");", found.NewText);
        Assert.Contains("отступы приведены к стилю файла", found.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public void OverEscapedQuotes_AreUnescaped_WithWarning()
    {
        var found = FragmentLocator.Locate(Code, "Say(\\\"hi\\\");", "Say(\\\"bye\\\");", "A.cs");

        Assert.Equal("Say(\"hi\");", Code.Substring(found.Offset, found.Length));
        Assert.Equal("Say(\"bye\");", found.NewText);
        Assert.Contains("экранированы лишний раз", found.Warning, StringComparison.Ordinal);
    }

    // The file has a dash and a non-breaking space; the model writes them in ASCII (Codex: seek_sequence).
    [Fact]
    public void Typography_IsIgnored_UnchangedLinesKeepFileCharacters()
    {
        const string text = "// Расчёт — по ставке\nvar total = price * rate;\n";

        var found = FragmentLocator.Locate(text, "// Расчёт - по ставке\nvar total = price * rate;", "// Расчёт - по ставке\nvar total = price * rate * 2;", "A.cs");

        Assert.Equal((0, text.Length - 1), (found.Offset, found.Length));
        Assert.Equal("// Расчёт — по ставке\nvar total = price * rate * 2;", found.NewText);
        Assert.Contains("тире, кавычек и особых пробелов (строки 1–2)", found.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Typography_ComparesPlainCharacters()
    {
        Assert.True(Typography.PlainEquals("«Да» — ‘нет’", "\"Да\" - 'нет'"));
        Assert.False(Typography.PlainEquals("a — b", "a — c"));
    }

    [Fact]
    public void Missing_ShowsSimilarPlace()
    {
        var error = Assert.Throws<AgentToolException>(() => FragmentLocator.Locate(Code, "void Run()\n{\n    Shout();", "x", "A.cs"));

        Assert.Contains("Похожее место начинается в строке 3", error.Message, StringComparison.Ordinal);
        Assert.Contains("3\t    void Run()", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_WithoutAnchor_AsksToReread()
    {
        var error = Assert.Throws<AgentToolException>(() => FragmentLocator.Locate(Code, "Shout();", "x", "A.cs"));

        Assert.Contains("Прочитайте файл заново", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ambiguous_ByLines_ListsLines()
    {
        const string text = "if (a)\n    Go();\nif (a)\n  Go();\n";

        var error = Assert.Throws<AgentToolException>(() => FragmentLocator.Locate(text, "if (a)\n        Go();", "x", "A.cs"));

        Assert.Contains("встречается несколько раз (строки 1, 3)", error.Message, StringComparison.Ordinal);
    }
}
