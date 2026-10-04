using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Storage;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Core.Tests.Keybindings;

public sealed class UserKeybindingsTests : IDisposable
{
    private static readonly string UserData = Path.GetFullPath(@"C:\user");
    private static readonly string File = Path.Combine(UserData, UserKeybindings.FileName);

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(UserData);
    private readonly KeybindingRegistry _registry = new();
    private readonly KeybindingResolver _resolver;
    private readonly ContextKeyService _context = new();
    private readonly UserKeybindings _user;

    public UserKeybindingsTests()
    {
        _resolver = new KeybindingResolver(_registry);
        _registry.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+K Ctrl+T"), "theme.toggle"));
        _registry.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+P"), "quickOpen"));
        _registry.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+E"), "quickOpen"));
        _user = new UserKeybindings(_registry, _fileSystem, new UserDataPaths(UserData), new InlineUiDispatcher(), NullLogger<UserKeybindings>.Instance);
    }

    public void Dispose() => _user.Dispose();

    [Fact]
    public void UserRule_OverridesDefaultKey_VsCodeNotation()
    {
        Write("""
            // свои сочетания
            [
                { "key": "ctrl+p", "command": "palette", "when": "!editorFocus" },
                { "key": "ctrl+shift+d", "command": "theme.toggle", "args": 5 },
            ]
            """);

        _user.Start();

        Assert.Equal("palette", Resolve("Ctrl+P"));
        Assert.Equal(5, Command("Ctrl+Shift+D")?.Argument);

        _context.Set("editorFocus", true);
        Assert.Equal("quickOpen", Resolve("Ctrl+P"));
    }

    [Fact]
    public void RemovalRule_HidesDefault_AndItComesBackWhenRuleIsGone()
    {
        Write("[ { \"key\": \"ctrl+k ctrl+t\", \"command\": \"-theme.toggle\" }, { \"command\": \"-quickOpen\" } ]");
        _user.Start();

        Assert.Null(_registry.FindForCommand("theme.toggle"));
        Assert.Null(_registry.FindForCommand("quickOpen"));
        Assert.Equal(KeyResolutionKind.NotHandled, _resolver.Resolve(Chord("Ctrl+E"), _context).Kind);

        Write("[]");
        _fileSystem.Watchers[^1].Raise(new FileChange(File, FileChangeKind.Changed));

        Assert.Equal(KeySequence.Parse("Ctrl+K Ctrl+T"), _registry.FindForCommand("theme.toggle")?.Sequence);
        Assert.Equal("quickOpen", Resolve("Ctrl+E"));
    }

    [Fact]
    public void RemovalAndRebind_MovesCommandToNewKey()
    {
        Write("[ { \"command\": \"-theme.toggle\" }, { \"key\": \"f9\", \"command\": \"theme.toggle\" } ]");

        _user.Start();

        Assert.Equal(KeySequence.Parse("F9"), _registry.FindForCommand("theme.toggle")?.Sequence);
    }

    [Fact]
    public void InvalidRules_AreSkipped_WithErrors()
    {
        Write("""
            [
                { "key": "ctrl+nope", "command": "a" },
                { "key": "ctrl+j" },
                { "key": "ctrl+l", "command": "b", "when": "a &&" },
                "строка",
                { "key": "ctrl+m", "command": "ok" }
            ]
            """);

        _user.Start();

        Assert.Equal(4, _user.Errors.Count);
        Assert.Equal("ok", Resolve("Ctrl+M"));
    }

    [Fact]
    public void BrokenFile_ReportsError_AndKeepsDefaults()
    {
        Write("[ { \"key\": ");

        _user.Start();

        Assert.Single(_user.Errors);
        Assert.Equal("quickOpen", Resolve("Ctrl+P"));
    }

    [Fact]
    public void EnsureFile_WritesTemplateThatParses()
    {
        _user.EnsureFile();
        _user.Reload();

        Assert.Empty(_user.Errors);
    }

    private void Write(string json) => _fileSystem.AddFile(File, json);

    private static KeyChord Chord(string text) => KeySequence.Parse(text).First;

    private string? Resolve(string keys) => Command(keys)?.CommandId;

    private KeybindingDefinition? Command(string keys) =>
        _resolver.Resolve(Chord(keys), _context) is { Kind: KeyResolutionKind.Command } resolution ? resolution.Binding : null;
}
