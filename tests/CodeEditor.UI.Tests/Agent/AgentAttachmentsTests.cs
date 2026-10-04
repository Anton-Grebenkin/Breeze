using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// Agent message attachments: the paperclip opens the system dialog, the chosen file shows as a chip above the input,
/// the cross removes it; the question sends the file content to the model and shows the file name under it.
/// </summary>
public sealed class AgentAttachmentsTests : IDisposable
{
    // The "File name" box of the standard Windows open dialog.
    private const string FileNameBox = "1148";

    private readonly FakeOpenAIServer _model = new();
    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _folder;
    private readonly string _notes;

    public AgentAttachmentsTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        _notes = Path.Combine(_folder, "notes.txt");
        File.WriteAllText(_notes, "Level up is a palindrome.");

        // The agent talks to a local fake model; the key is a test key that never leaves this server.
        File.WriteAllText(Path.Combine(_userData, "settings.json"),
            $$"""{ "agent.endpoint": "{{_model.Endpoint}}", "agent.model": "test/model", "agent.autoMemory": false }""");
        new DpapiSecretStore(new PhysicalFileSystem(), new UserDataPaths(_userData)).Set("agent-api-key", "test-key");
    }

    public void Dispose()
    {
        _model.Dispose();
        AppSession.DeleteQuietly(_userData);
        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void Paperclip_PicksFile_CrossRemovesIt_QuestionCarriesIt()
    {
        _model.Reply("Первое слово — палиндром.");
        using var session = AppSession.WithUserData(_userData, _folder);
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
        session.WaitForFocus("Agent.Input");

        Attach(session);
        session.Find("Agent.Attachment.notes.txt.Remove").GuardedClick();
        session.WaitUntilGone("Agent.Attachment.notes.txt");

        Attach(session);
        session.SaveScreenshot("agent-attachments");
        session.Find("Agent.Input").GuardedClick();
        session.TypeInto("Agent.Input", "Что в файле?");
        Keyboard.Type(VirtualKeyShort.ENTER);

        session.WaitForText("Agent.Message.Files", text => text.Contains("notes.txt", StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        session.WaitUntilGone("Agent.Attachment.notes.txt");
        Assert.True(Retry.WhileFalse(() => !_model.Requests.IsEmpty, TimeSpan.FromSeconds(10)).Result, "запрос не дошёл до модели");
        Assert.Contains("Level up is a palindrome.", _model.Requests.Single(), StringComparison.Ordinal);
    }

    // Paperclip → system dialog → path into "File name" (focused on open) → Enter → chip above the input.
    // The box id belongs to a combo box whose inner edit has focus, so wait for it to appear rather than for focus.
    private void Attach(AppSession session)
    {
        session.Find("Agent.Attach").GuardedClick();
        session.WaitFor(FileNameBox);
        Keyboard.Type(_notes);
        Keyboard.Type(VirtualKeyShort.ENTER);
        session.WaitFor("Agent.Attachment.notes.txt");
    }
}
