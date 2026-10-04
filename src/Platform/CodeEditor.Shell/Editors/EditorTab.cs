using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Editor area tab (ADR 0031): a text document (<see cref="EditorTabViewModel"/>), a viewer for a non-text file, or a
/// view without a file such as a git diff, container log or browser (<see cref="ViewTabViewModel"/>). The content
/// (<see cref="Editor"/>) is a ViewModel; the view is resolved by its type.
/// </summary>
public abstract partial class EditorTab : ObservableObject, IDisposable
{
    protected EditorTab(string key, object editor, bool isPreview)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(editor);
        Key = key;
        Editor = editor;
        IsPreview = isPreview;
        if (editor is IPendingReview review)
        {
            review.PropertyChanged += OnEditorPropertyChanged;
        }
    }

    /// <summary>Full file path or view id; used to find the tab again.</summary>
    public string Key { get; }

    /// <summary>ViewModel of the tab content.</summary>
    public object Editor { get; }

    public abstract string Title { get; }

    public abstract string ToolTip { get; }

    /// <summary>The tab's file; <c>null</c> for a view without a file.</summary>
    public abstract string? FilePath { get; }

    public virtual bool IsDirty => false;

    /// <summary>The file was changed on disk over unsaved edits, or deleted.</summary>
    public virtual bool HasProblem => false;

    /// <summary>The content has changes awaiting review (<see cref="IPendingReview"/>).</summary>
    public bool HasPendingReview => Editor is IPendingReview { HasPendingReview: true };

    public string AutomationId => $"EditorTab.{Title}";

    [ObservableProperty]
    public partial bool IsPreview { get; set; }

    /// <summary>The active tab of the whole area, where the user works.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>The tab is visible in its group; each group shows one tab.</summary>
    [ObservableProperty]
    public partial bool IsShown { get; set; }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Called when the tab closes: unsubscribes from events and disposes the content.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Editor is IPendingReview review)
            {
                review.PropertyChanged -= OnEditorPropertyChanged;
            }

            (Editor as IDisposable)?.Dispose();
        }
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPendingReview.HasPendingReview))
        {
            OnPropertyChanged(nameof(HasPendingReview));
        }
    }
}
