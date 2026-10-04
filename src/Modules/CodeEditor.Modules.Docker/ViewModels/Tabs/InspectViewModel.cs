using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Shell.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Docker.ViewModels.Tabs;

/// <summary>
/// Container or image details (<c>docker inspect</c>) in an editor tab: JSON with secret-looking environment values
/// hidden (<see cref="InspectSecrets"/>), since the tab is visible on screen and in screen sharing. Closing the tab
/// cancels the read.
/// </summary>
public sealed partial class InspectViewModel(string title, IReadOnlyList<string> arguments, DockerRunner docker, ISystemShell shell)
    : ObservableObject, IDisposable
{
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _lifetime = new();

    public string Title { get; } = title;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    public partial string Text { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var result = await docker.RunForPanelAsync(arguments, ReadTimeout, onLine: null, _lifetime.Token);
            if (result.ExitCode == 0 && !result.TimedOut)
            {
                Text = InspectSecrets.Hide(result.Output.TrimEnd());
            }
            else
            {
                Error = DockerErrors.FirstLine(result.Output) is { Length: > 0 } line ? line : Strings.InspectFailed;
            }
        }
        catch (InvalidOperationException exception)
        {
            Error = exception.Message;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The tab was closed.
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Copy() => shell.CopyToClipboard(Text);

    private bool CanCopy() => Text.Length > 0;

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
