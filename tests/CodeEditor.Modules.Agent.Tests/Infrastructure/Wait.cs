namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>
/// Waits for a condition set by a thread-pool continuation (a question answer, a background turn end): polls for up to
/// 5 s, then asserts. Under full-suite load the continuation can land after the call under test returns.
/// </summary>
internal static class Wait
{
    private const int Attempts = 500;

    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(10);

    public static async Task UntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < Attempts && !Holds(condition); attempt++)
        {
            await Task.Delay(Step, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    // The turn thread may change the chat feed while the condition enumerates it: that poll counts as "not yet".
    private static bool Holds(Func<bool> condition)
    {
        try
        {
            return condition();
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
