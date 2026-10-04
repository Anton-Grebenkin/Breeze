using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Tests.ViewModels;

public sealed class StatusBarViewModelTests
{
    [Fact]
    public void Message_ByDefault_IsReady()
    {
        var statusBar = new StatusBarViewModel();

        Assert.Equal(StatusBarViewModel.ReadyMessage, statusBar.Message);
    }

    [Fact]
    public void Message_WhenChanged_RaisesPropertyChanged()
    {
        var statusBar = new StatusBarViewModel();
        var changed = new List<string?>();
        statusBar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        statusBar.Message = "Запуск: 100 мс";

        Assert.Equal([nameof(StatusBarViewModel.Message)], changed);
    }
}
