using CodeEditor.Core.Keybindings;

namespace CodeEditor.Core.Tests.Keybindings;

public sealed class KeyGestureParserTests
{
    [Theory]
    [InlineData("Ctrl+Shift+P", KeyModifiers.Ctrl | KeyModifiers.Shift, KeyCode.P)]
    [InlineData("F1", KeyModifiers.None, KeyCode.F1)]
    [InlineData("ctrl+s", KeyModifiers.Ctrl, KeyCode.S)]
    [InlineData("Shift+Ctrl+P", KeyModifiers.Ctrl | KeyModifiers.Shift, KeyCode.P)]
    [InlineData("Control+Alt+Del", KeyModifiers.Ctrl | KeyModifiers.Alt, KeyCode.Delete)]
    [InlineData("Esc", KeyModifiers.None, KeyCode.Escape)]
    [InlineData("Enter", KeyModifiers.None, KeyCode.Enter)]
    [InlineData("Return", KeyModifiers.None, KeyCode.Enter)]
    [InlineData("PageUp", KeyModifiers.None, KeyCode.PageUp)]
    [InlineData("PgDn", KeyModifiers.None, KeyCode.PageDown)]
    [InlineData("Ctrl+1", KeyModifiers.Ctrl, KeyCode.D1)]
    [InlineData("Ctrl+/", KeyModifiers.Ctrl, KeyCode.Slash)]
    [InlineData("Ctrl+-", KeyModifiers.Ctrl, KeyCode.Minus)]
    [InlineData("Ctrl++", KeyModifiers.Ctrl, KeyCode.EqualsSign)]
    [InlineData("Ctrl+Shift++", KeyModifiers.Ctrl | KeyModifiers.Shift, KeyCode.EqualsSign)]
    [InlineData("+", KeyModifiers.None, KeyCode.EqualsSign)]
    [InlineData("Win+E", KeyModifiers.Win, KeyCode.E)]
    public void Parse_SingleChord(string text, KeyModifiers modifiers, KeyCode key)
    {
        var sequence = KeyGestureParser.Parse(text);

        Assert.Equal(new KeySequence(new KeyChord(modifiers, key)), sequence);
        Assert.False(sequence.IsChord);
    }

    [Fact]
    public void Parse_TwoChords()
    {
        var sequence = KeyGestureParser.Parse("Ctrl+K  Ctrl+O");

        Assert.True(sequence.IsChord);
        Assert.Equal(new KeyChord(KeyModifiers.Ctrl, KeyCode.K), sequence.First);
        Assert.Equal(new KeyChord(KeyModifiers.Ctrl, KeyCode.O), sequence.Second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Foo")]
    [InlineData("Hyper+P")]
    [InlineData("Ctrl+K Ctrl+O Ctrl+X")]
    [InlineData("Ctrl++P")]
    public void Parse_Invalid_Throws(string text)
    {
        Assert.Throws<FormatException>(() => KeyGestureParser.Parse(text));
        Assert.False(KeyGestureParser.TryParse(text, out _));
    }

    [Theory]
    [InlineData("ctrl+shift+p", "Ctrl+Shift+P")]
    [InlineData("alt+ctrl+del", "Ctrl+Alt+Del")]
    [InlineData("Ctrl+K Ctrl+O", "Ctrl+K Ctrl+O")]
    [InlineData("Ctrl+D1", "Ctrl+1")]
    [InlineData("Ctrl+Slash", "Ctrl+/")]
    [InlineData("F12", "F12")]
    [InlineData("Escape", "Esc")]
    public void ToString_UsesCanonicalForm(string text, string expected)
    {
        var sequence = KeyGestureParser.Parse(text);

        Assert.Equal(expected, sequence.ToString());
        Assert.Equal(sequence, KeyGestureParser.Parse(sequence.ToString()));
    }
}
