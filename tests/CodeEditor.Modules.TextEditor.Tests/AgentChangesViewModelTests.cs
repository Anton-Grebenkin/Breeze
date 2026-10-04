using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.TextEditor.Tests;

/// <summary>
/// Agent changes in an open file: hunks appear after an agent edit and are accepted or rejected one by one;
/// "accept all" saves the file; user typing recomputes hunks after a delay.
/// </summary>
public sealed class AgentChangesViewModelTests : IDisposable
{
    private const string Original = "a\nb\nc\nd\n";
    private static readonly string Path = EditorFixture.PathOf("src/A.cs");

    private readonly EditorFixture _fixture = new();

    public AgentChangesViewModelTests() => _fixture.FileSystem.AddFile(Path, Original);

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task AgentEdit_ShowsHunks_AcceptOne_RejectOther()
    {
        var (editor, changes) = await OpenAsync();
        AgentEdits(editor, "a\nB\nc\nx\nd\n");

        Assert.Equal(2, changes.Hunks.Count);
        Assert.Equal("Правки агента: 2 · +2 −1", changes.Summary);

        await changes.AcceptHunkCommand.ExecuteAsync(changes.Hunks[0]);
        var remaining = Assert.Single(changes.Hunks);
        Assert.Equal(4, remaining.NewStart);
        Assert.Equal("a\nB\nc\nd\n", _fixture.FileState.Changes[Path]);

        changes.RejectHunkCommand.Execute(remaining);
        Assert.Empty(changes.Hunks);
        Assert.Equal("a\nB\nc\nd\n", editor.Document.Buffer.GetText());
        // The file matches the original again, so it leaves the chat's change list.
        Assert.False(_fixture.FileState.Changes.ContainsKey(Path));
        // The model learns about the rejection at the start of the next message.
        Assert.Equal([Path], _fixture.FileState.TakeRejected());
    }

    [Fact]
    public async Task AcceptLastHunk_AcceptsFile_AndSaves()
    {
        var (editor, changes) = await OpenAsync();
        AgentEdits(editor, "a\nB\nc\nd\n");

        await changes.AcceptHunkCommand.ExecuteAsync(Assert.Single(changes.Hunks));

        Assert.False(changes.HasChanges);
        Assert.False(_fixture.FileState.Changes.ContainsKey(Path));
        Assert.False(editor.Document.IsDirty);
        Assert.Equal("a\nB\nc\nd\n", _fixture.FileSystem.ReadAllText(Path));
    }

    [Fact]
    public async Task RejectAll_RevertsThroughReverter_AndForgetsTheFile()
    {
        var (editor, changes) = await OpenAsync();
        AgentEdits(editor, "a\nB\nc\nd\n");

        await changes.RejectAllCommand.ExecuteAsync(null);

        Assert.Equal([(Path, Original)], _fixture.Reverter.Reverted);
        Assert.False(_fixture.FileState.Changes.ContainsKey(Path));
        Assert.Equal([Path], _fixture.FileState.TakeRejected());
    }

    [Fact]
    public async Task UserTyping_RecomputesAfterDelay()
    {
        var (editor, changes) = await OpenAsync();
        AgentEdits(editor, "a\nB\nc\nd\n");
        Assert.Single(changes.Hunks);

        editor.Document.Buffer.Replace(0, 0, "// note\n");
        Assert.Single(changes.Hunks);
        _fixture.Time.Advance(AgentChangesViewModel.TypingDelay);

        Assert.Equal([1, 3], changes.Hunks.Select(hunk => hunk.NewStart));
    }

    [Fact]
    public async Task Navigation_GoesToNextAndPreviousHunk()
    {
        var (editor, changes) = await OpenAsync();
        AgentEdits(editor, "A\nb\nc\nD\n");

        changes.NextCommand.Execute(1);
        Assert.Equal(4, editor.TakeNavigation()?.Location?.Line);
        changes.PreviousCommand.Execute(4);
        Assert.Equal(1, editor.TakeNavigation()?.Location?.Line);
        Assert.Equal(changes.Hunks[1], changes.HunkAt(3));
    }

    // Agent edits are saved at once (ADR 0040): rejecting a hunk writes the file again, unsaved typing stays unsaved.
    [Fact]
    public async Task RejectHunk_SavesFileThatMatchedTheDisk()
    {
        var (editor, changes) = await OpenAsync();
        AgentEdits(editor, "a\nB\nc\nd\n");
        await _fixture.Documents.SaveAsync(editor.Document, TestContext.Current.CancellationToken);

        await changes.RejectHunkCommand.ExecuteAsync(Assert.Single(changes.Hunks));

        Assert.False(editor.Document.IsDirty);
        Assert.Equal(Original, _fixture.FileSystem.ReadAllText(Path));
    }

    [Fact]
    public async Task Tab_IsHighlighted_WhileEditsAwaitReview()
    {
        var tab = await _fixture.Editors.OpenTextAsync(new OpenFileRequest(Path));
        var editor = (TextEditorViewModel)tab!.Editor;
        Assert.False(tab.HasPendingReview);

        AgentEdits(editor, "a\nB\nc\nd\n");
        Assert.True(tab.HasPendingReview);

        await editor.AgentChanges!.AcceptAllCommand.ExecuteAsync(null);
        Assert.False(tab.HasPendingReview);
    }

    private async Task<(TextEditorViewModel Editor, AgentChangesViewModel Changes)> OpenAsync()
    {
        var tab = await _fixture.Editors.OpenTextAsync(new OpenFileRequest(Path));
        var editor = (TextEditorViewModel)tab!.Editor;
        return (editor, editor.AgentChanges!);
    }

    // Like the edit tool: new text into the tab, the original into the file state.
    private void AgentEdits(TextEditorViewModel editor, string text)
    {
        var buffer = editor.Document.Buffer;
        buffer.Replace(0, buffer.Length, text);
        _fixture.FileState.RecordWrite(Path, text, Original);
    }
}
