using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Integration;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Settings;
using CodeEditor.Testing;

namespace CodeEditor.Shell.Tests.Integration;

public sealed class FileTypesPromptTests
{
    private readonly RecordingNotifications _notifications = new();
    private readonly TestOptionsMonitor<WindowsIntegrationOptions> _options = new(new WindowsIntegrationOptions());

    [Fact]
    public void Undecided_InstalledBuild_AsksOnce_WithCommandButtons()
    {
        using var prompt = Prompt(installed: true);

        prompt.ShowIfUndecided();
        prompt.ShowIfUndecided();

        var shown = Assert.Single(_notifications.Shown);
        Assert.Equal(FileTypesPrompt.NotificationId, shown.Id);
        Assert.Equal(
            [WindowsIntegrationCommands.DisableFileTypesId, WindowsIntegrationCommands.EnableFileTypesId],
            shown.Actions.Select(action => action.CommandId));
        Assert.Equal([false, true], shown.Actions.Select(action => action.IsPrimary));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Answered_DoesNotAsk(bool answer)
    {
        _options.Set(new WindowsIntegrationOptions { FileTypes = answer });
        using var prompt = Prompt(installed: true);

        prompt.ShowIfUndecided();

        Assert.Empty(_notifications.Shown);
    }

    // Portable and development builds register nothing, so there is nothing to agree to.
    [Fact]
    public void NotInstalled_DoesNotAsk()
    {
        using var prompt = Prompt(installed: false);

        prompt.ShowIfUndecided();

        Assert.Empty(_notifications.Shown);
    }

    // The answer may come from another window, the palette or settings.json.
    [Fact]
    public void AnswerFromAnywhere_ClosesTheQuestion()
    {
        using var prompt = Prompt(installed: true);
        prompt.ShowIfUndecided();

        _options.Set(new WindowsIntegrationOptions { ContextMenu = false });
        Assert.Equal(0, _notifications.Closed);

        _options.Set(new WindowsIntegrationOptions { FileTypes = false });
        Assert.Equal(1, _notifications.Closed);
    }

    private FileTypesPrompt Prompt(bool installed) => new(_notifications, new FakeIntegration(installed), _options);

    private sealed class FakeIntegration(bool installed) : IWindowsIntegration
    {
        public bool IsAvailable => installed;
    }

    private sealed class RecordingNotifications : INotificationService
    {
        public List<Notification> Shown { get; } = [];

        public int Closed { get; private set; }

        public IDisposable Show(Notification notification)
        {
            Shown.Add(notification);
            return new Closing(() => Closed++);
        }

        private sealed class Closing(Action close) : IDisposable
        {
            private Action? _close = close;

            public void Dispose() => Interlocked.Exchange(ref _close, null)?.Invoke();
        }
    }
}
