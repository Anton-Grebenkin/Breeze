using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.ViewModels;

public sealed record NotificationButtonViewModel(string Title, string AutomationId, bool IsPrimary, IAsyncRelayCommand Command);
