using CodeEditor.Modules.Agent.Services.Attachments;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Tools;

/// <summary>Message attachments: text goes as content, images as pictures, anything else isn't attached.</summary>
public sealed class AttachmentReaderTests : IDisposable
{
    private const string Code = "class A\n{\n}\n";

    private readonly AgentFixture _fixture = new();
    private readonly string _code = Path.Combine(AgentFixture.Root, "src", "A.cs");
    private readonly string _image = Path.Combine(AgentFixture.Root, "shot.png");

    public AttachmentReaderTests()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.FileSystem.AddFile(_code, Code).AddBytes(_image, [0x89, 0x50, 0x4E, 0x47]);
    }

    public void Dispose() => _fixture.Dispose();

    private AttachmentReader Reader => _fixture.Attachments;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Classify_TextAndImages_RestIsUnsupported()
    {
        var binary = Path.Combine(AgentFixture.Root, "data.txt");
        var pdf = Path.Combine(AgentFixture.Root, "spec.pdf");
        _fixture.FileSystem.AddBytes(binary, [0x41, 0x00, 0x42]).AddFile(pdf, "%PDF-1.7");

        Assert.Equal(AttachmentKind.Text, await Reader.ClassifyAsync(_code, Token));
        Assert.Equal(AttachmentKind.Image, await Reader.ClassifyAsync(_image, Token));
        Assert.Equal(AttachmentKind.Unsupported, await Reader.ClassifyAsync(binary, Token));
        Assert.Equal(AttachmentKind.Unsupported, await Reader.ClassifyAsync(pdf, Token));
        Assert.Equal(AttachmentKind.Unsupported, await Reader.ClassifyAsync(Path.Combine(AgentFixture.Root, "missing.txt"), Token));
    }

    // The model saw the whole attached folder file, so it may edit it without reading it again.
    [Fact]
    public async Task TextFile_GoesAsBlock_AndCountsAsRead()
    {
        var block = Assert.IsType<TextContent>(Assert.Single(await Reader.ReadAsync([_code], Token))).Text;

        Assert.Equal("<attachment path=\"src/A.cs\">\n" + Code + "\n</attachment>", block);
        Assert.Null(_fixture.FileState.CheckEditable(_code, "src/A.cs", Code));
    }

    [Fact]
    public async Task LongText_IsCut_WithLineCounts()
    {
        var log = Path.Combine(AgentFixture.Root, "big.log");
        _fixture.FileSystem.AddFile(log, string.Concat(Enumerable.Repeat(new string('x', 99) + "\n", 2_000)));

        var block = Assert.IsType<TextContent>(Assert.Single(await Reader.ReadAsync([log], Token))).Text;

        Assert.StartsWith("<attachment path=\"big.log\" shown=\"lines 1-1000 of 2000\">", block, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Image_GoesAsPicture_AfterItsName()
    {
        var contents = await Reader.ReadAsync([_image], Token);

        Assert.Equal("<attachment path=\"shot.png\" type=\"image\" />", Assert.IsType<TextContent>(contents[0]).Text);
        Assert.Equal("image/png", Assert.IsType<DataContent>(contents[1]).MediaType);
    }

    [Fact]
    public async Task FileOutsideFolder_KeepsFullPath_MissingFileIsNoted()
    {
        var outside = Path.GetFullPath(@"C:\other\notes.txt");
        _fixture.FileSystem.AddFile(outside, "заметки");

        var contents = await Reader.ReadAsync([outside, Path.Combine(AgentFixture.Root, "gone.txt")], Token);

        Assert.StartsWith($"<attachment path=\"{outside}\">", Assert.IsType<TextContent>(contents[0]).Text, StringComparison.Ordinal);
        Assert.Contains("not attached", Assert.IsType<TextContent>(contents[1]).Text, StringComparison.Ordinal);
    }
}
