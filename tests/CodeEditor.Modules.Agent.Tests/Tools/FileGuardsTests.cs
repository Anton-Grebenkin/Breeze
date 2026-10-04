using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;

namespace CodeEditor.Modules.Agent.Tests.Tools;

/// <summary>Path guards and chat file state shared by the tools of all modules.</summary>
public sealed class FileGuardsTests
{
    [Theory]
    [InlineData(".env", true)]
    [InlineData("src/.env.local", true)]
    [InlineData("keys/id_rsa", true)]
    [InlineData("certs/site.pfx", true)]
    [InlineData("secrets.json", true)]
    [InlineData(".env.example", false)]
    [InlineData("appsettings.json", false)]
    [InlineData("src/Environment.cs", false)]
    public void Secrets_AreRecognized(string path, bool secret) => Assert.Equal(secret, SensitivePaths.IsSecret(path));

    [Theory]
    [InlineData("src/App/bin/Debug/App.dll", true)]
    [InlineData("obj/project.assets.json", true)]
    [InlineData(".git/config", true)]
    [InlineData(".breeze/agent/chat-1.json", true)]
    [InlineData(".breeze/instructions.md", false)]
    [InlineData("src/bin.cs", false)]
    [InlineData("docs/objects.md", false)]
    public void ProtectedFolders_AreRecognized(string path, bool isProtected) => Assert.Equal(isProtected, SensitivePaths.IsProtected(path));

    [Fact]
    public void Guards_ExplainWhatToDo()
    {
        Assert.Contains("попросите пользователя", Assert.Throws<AgentToolException>(() => SensitivePaths.EnsureReadable(".env")).Message, StringComparison.Ordinal);
        Assert.Contains("служебная папка", Assert.Throws<AgentToolException>(() => SensitivePaths.EnsureWritable("bin/a.txt")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_RequiresRead_AndCurrentVersion()
    {
        var state = new AgentFileState();
        Assert.Contains("ещё не прочитан", state.CheckEditable(@"C:\r\a.cs", "a.cs", "v1"), StringComparison.Ordinal);

        state.RecordRead(@"C:\r\a.cs", "v1", 1, 10);
        Assert.Null(state.CheckEditable(@"C:\R\A.CS", "a.cs", "v1"));
        Assert.Contains("изменился после вашего последнего чтения", state.CheckEditable(@"C:\r\a.cs", "a.cs", "v2"), StringComparison.Ordinal);

        state.RecordWrite(@"C:\r\a.cs", "v2", "v1");
        state.RecordWrite(@"C:\r\a.cs", "v3", "v2");
        Assert.Equal("v1", state.Changes[@"C:\r\a.cs"]);
        state.RecordWrite(@"C:\r\a.cs", "v2", "v3");
        Assert.Null(state.CheckEditable(@"C:\r\a.cs", "a.cs", "v2"));

        state.Reset();
        Assert.NotNull(state.CheckEditable(@"C:\r\a.cs", "a.cs", "v2"));
    }

    [Fact]
    public void RepeatedWindow_OfSameVersion_IsReported()
    {
        var state = new AgentFileState();

        Assert.False(state.RecordRead(@"C:\r\a.cs", "v1", 1, 400));
        Assert.True(state.RecordRead(@"C:\r\a.cs", "v1", 1, 400));
        Assert.False(state.RecordRead(@"C:\r\a.cs", "v1", 401, 800));
        Assert.False(state.RecordRead(@"C:\r\a.cs", "v2", 1, 400));
    }

    // Asking for the same window after a pointer to the earlier result means the text is needed; point back only once.
    [Fact]
    public void ThirdReadOfSameWindow_GetsText()
    {
        var state = new AgentFileState();

        Assert.Equal([false, true, false, false], Enumerable.Range(0, 4).Select(_ => state.RecordRead(@"C:\r\a.cs", "v1", 1, 400)));
    }

    // Accepting a hunk updates the diff's original, accepting a file drops it; editors are notified of both.
    [Fact]
    public void OriginalUpdates_RaiseChangesChanged()
    {
        var state = new AgentFileState();
        var raised = new List<string?>();
        state.ChangesChanged += (_, path) => raised.Add(path);
        state.RecordWrite(@"C:\r\a.cs", "v2", "v1");

        state.UpdateOriginal(@"C:\r\a.cs", "v1.5");
        Assert.Equal("v1.5", state.Changes[@"C:\r\a.cs"]);

        state.AcceptFile(@"C:\r\a.cs");
        Assert.Empty(state.Changes);
        state.AcceptChanges();
        state.RecordWrite(@"C:\r\b.cs", "v2", "v1");
        state.Reset();

        // A new chat forgets reads but keeps edits awaiting review.
        Assert.Equal([@"C:\r\a.cs", @"C:\r\a.cs", null], raised);
        Assert.Equal("v1", state.Changes[@"C:\r\b.cs"]);
    }

    // The explorer and the agent read the same windows concurrently: each gets text, not "unchanged", and the explorer's
    // reads don't count for the agent.
    [Fact]
    public async Task IsolatedReads_DoNotAffectConcurrentAgentReads()
    {
        var state = new AgentFileState();
        using var explorerRead = new SemaphoreSlim(0);
        using var agentRead = new SemaphoreSlim(0);

        var explorer = Task.Run(async () =>
        {
            using var isolation = state.IsolateReads();
            Assert.False(state.RecordRead(@"C:\r\a.cs", "v1", 1, 400));
            explorerRead.Release();
            await agentRead.WaitAsync(TestContext.Current.CancellationToken);
            Assert.True(state.RecordRead(@"C:\r\a.cs", "v1", 1, 400));
        }, TestContext.Current.CancellationToken);

        await explorerRead.WaitAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(state.CheckEditable(@"C:\r\a.cs", "a.cs", "v1"));
        Assert.False(state.RecordRead(@"C:\r\a.cs", "v1", 1, 400));
        agentRead.Release();
        await explorer;

        Assert.Null(state.CheckEditable(@"C:\r\a.cs", "a.cs", "v1"));
        Assert.True(state.RecordRead(@"C:\r\a.cs", "v1", 1, 400));
    }
}
