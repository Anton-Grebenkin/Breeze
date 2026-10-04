using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.Modules.Agent.ViewModels.Composer;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Chat;

/// <summary>Composer attachments: picking, repeats, unsupported files, sending with the question.</summary>
public sealed class ChatAttachmentsTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private readonly string _code = Path.Combine(AgentFixture.Root, "src", "A.cs");
    private readonly string _image = Path.Combine(AgentFixture.Root, "shot.png");
    private readonly string _archive = Path.Combine(AgentFixture.Root, "app.zip");

    public ChatAttachmentsTests()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.FileSystem.AddFile(_code, "class A { }").AddBytes(_image, [0x89, 0x50, 0x4E, 0x47]).AddBytes(_archive, [0x50, 0x4B, 0x00]);
    }

    public void Dispose() => _fixture.Dispose();

    private ChatAttachmentsViewModel Attachments => _fixture.Chat.Attachments;

    [Fact]
    public async Task Pick_AddsTextAndImage_SkipsRepeatsAndUnsupported()
    {
        _fixture.AttachmentPicker.Files.AddRange([_code, _image, _archive, _code]);

        await Attachments.PickCommand.ExecuteAsync(null);
        await Attachments.PickCommand.ExecuteAsync(null);

        Assert.Equal(["A.cs", "shot.png"], Attachments.Items.Select(item => item.Name));
        Assert.True(Attachments.Items[1].IsImage);
        Assert.Contains("app.zip", _fixture.StatusBar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Remove_TakesTheChipAway()
    {
        await Attachments.AddAsync([_code, _image]);

        Attachments.Items[0].RemoveCommand.Execute(null);

        Assert.Equal(["shot.png"], Attachments.Items.Select(item => item.Name));
        Assert.True(Attachments.HasItems);
    }

    // Text goes as a block in the message, an image as a picture; the feed lists the names and the chips are cleared.
    [Fact]
    public async Task Send_PutsFilesIntoTheQuestion_AndShowsThemInTheFeed()
    {
        await Attachments.AddAsync([_code, _image]);
        _fixture.Client.Reply("Вижу.");

        await _fixture.SendAsync("что в файлах?");

        var question = _fixture.Client.Requests[^1].Last(message => message.Role == ChatRole.User);
        Assert.Contains(question.Contents.OfType<TextContent>(), content => content.Text.StartsWith("<attachment path=\"src/A.cs\">", StringComparison.Ordinal));
        Assert.Single(question.Contents.OfType<DataContent>());
        Assert.Equal("A.cs, shot.png", _fixture.Chat.Messages.First(message => message.Kind == ChatMessageKind.User).FilesText);
        Assert.Empty(Attachments.Items);
    }
}
