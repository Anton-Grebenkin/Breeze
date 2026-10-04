using System.ComponentModel;

namespace CodeEditor.Shell.Editors;

/// <summary>Tab content with changes awaiting the user's review, such as agent edits: its tab is highlighted.</summary>
public interface IPendingReview : INotifyPropertyChanged
{
    bool HasPendingReview { get; }
}
