using System.Globalization;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>
/// The agent's plan above the input box: a collapsible "Plan · 2 of 5" checklist. The tool changes the plan on a
/// background thread, so updates are posted to the UI thread.
/// </summary>
public sealed partial class TodoPanelViewModel : ObservableObject, IDisposable
{
    private readonly TodoList _todos;
    private readonly IUiDispatcher _dispatcher;

    public TodoPanelViewModel(TodoList todos, IUiDispatcher dispatcher)
    {
        _todos = todos;
        _dispatcher = dispatcher;
        _todos.Changed += OnTodosChanged;
    }

    public IReadOnlyList<TodoItem> Items => _todos.Items;

    public bool HasItems => !_todos.Items.IsEmpty;

    public string Title => string.Format(CultureInfo.CurrentCulture, Strings.TodoTitle, _todos.Items.Count(item => item.Status == TodoStatus.Completed), _todos.Items.Length);

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    public void Dispose() => _todos.Changed -= OnTodosChanged;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    private void OnTodosChanged(object? sender, EventArgs e) => _dispatcher.Post(() => OnPropertyChanged(string.Empty));
}
