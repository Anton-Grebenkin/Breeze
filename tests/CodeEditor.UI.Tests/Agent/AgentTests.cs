using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// Agent panel offline: <c>Ctrl+Alt+I</c> opens the chat, without a key it shows a hint, a button opens the masked key
/// input and <c>Esc</c> closes it.
/// </summary>
public sealed class AgentTests(AppSession session) : IClassFixture<AppSession>
{
    [Fact]
    public void Panel_WithoutKey_OffersToSetIt()
    {
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);

        session.WaitForFocus("Agent.Input");
        session.SaveScreenshot("agent-no-key");

        session.Find("Agent.SetApiKey").GuardedClick();
        session.WaitForFocus("SecretPrompt.Value");
        session.SaveScreenshot("agent-key-prompt");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        session.WaitUntilGone("SecretPrompt");
    }
}
