using CodeEditor.Shell.Services;

namespace CodeEditor.Shell.Instances;

public sealed class AppWindows(WindowRegistry registry) : IAppWindows
{
    /// <summary>A live window answers at once; a longer wait means it hangs, and the caller opens the folder itself.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);

    public int CountOthers() => registry.Others(Environment.ProcessId).Count;

    public void OpenNew(string? folder) => AppProcess.Start(folder is null ? [LaunchRequest.NewWindowFlag] : [folder]);

    public async Task<bool> TryActivateAsync(string folder) =>
        registry.Others(Environment.ProcessId).FirstOrDefault(window => LaunchRouter.SameFolder(window.Folder, folder)) is { } window
        && await InstanceClient.TrySendAsync(window, folder, RequestTimeout);
}
