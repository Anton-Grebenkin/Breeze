using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Tools;

/// <summary>
/// The agent views folder images (ADR 0030): <c>view_image</c> queues the image, the tool loop ends and the model gets
/// it in the next message like a user attachment; models without vision don't see the tool.
/// </summary>
public sealed class ImageToolsTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly string Mockup = Path.Combine(AgentFixture.Root, "docs", "mockup.png");

    private readonly AgentFixture _fixture = new();
    private readonly ImageAgentTools _tools;

    public ImageToolsTests()
    {
        _fixture.FileSystem.AddBytes(Mockup, Png).AddFile(Path.Combine(AgentFixture.Root, "notes.txt"), "text");
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.Options.Set(new AgentOptions { Model = "x-ai/grok-4.7" });
        _tools = new ImageAgentTools(_fixture.Workspace, _fixture.FileSystem, _fixture.Images);
        _fixture.ToolProviders.Add(_tools);
        _fixture.Presenters.Add(new ImageToolPresenter());
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ViewImage_ImageGoesToTheModel_InTheNextMessage()
    {
        _fixture.Client
            .CallTool("c1", ImageAgentTools.ViewImageName, new Dictionary<string, object?> { ["path"] = "docs/mockup.png" })
            .Reply("На макете кнопка «Купить».");

        await _fixture.SendAsync("что на макете?");

        var request = _fixture.Client.Requests[1];
        var result = Assert.Single(request[^2].Contents.OfType<FunctionResultContent>());
        var image = Assert.Single(request[^1].Contents.OfType<DataContent>());
        Assert.Equal("Картинка docs/mockup.png — в следующем сообщении.", result.Result?.ToString());
        Assert.Equal((ChatRole.User, "image/png"), (request[^1].Role, image.MediaType));
        Assert.Equal(Png, image.Data.ToArray());
        Assert.Contains("<image source=\"docs/mockup.png\" />", request[^1].Text, StringComparison.Ordinal);
        Assert.Contains(_fixture.Chat.Messages, message => message.Text == "Картинка docs/mockup.png");
        Assert.Null(_fixture.Images.Take());
    }

    [Theory]
    [InlineData("notes.txt", "не картинка")]
    [InlineData("docs/missing.png", "не найден")]
    [InlineData("../outside.png", "вне рабочей папки")]
    public async Task ViewImage_RefusesWhatTheModelCannotSee(string path, string reason)
    {
        var tool = _tools.CreateTools().OfType<AIFunction>().Single();

        var error = await Assert.ThrowsAsync<AgentToolException>(async () =>
            await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["path"] = path }), TestContext.Current.CancellationToken));

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
        Assert.Null(_fixture.Images.Take());
    }

    [Theory]
    [InlineData("anthropic/claude-sonnet-5.5", true)]
    [InlineData("openai/gpt-6-luna", true)]
    [InlineData("x-ai/grok-4.7", true)]
    [InlineData("deepseek/deepseek-v4.1-flash", false)]
    public void ViewImage_IsOfferedOnlyToModelsThatSeeImages(string model, bool offered) =>
        Assert.Equal(offered, ModelProfiles.For(model).Allows(ImageAgentTools.ViewImageName));
}
