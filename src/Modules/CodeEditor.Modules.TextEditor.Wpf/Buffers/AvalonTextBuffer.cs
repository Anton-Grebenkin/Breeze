using System.ComponentModel;
using CodeEditor.Core.Documents;
using ICSharpCode.AvalonEdit.Document;

namespace CodeEditor.Modules.TextEditor.Wpf.Buffers;

/// <summary>
/// <see cref="ITextBuffer"/> over AvalonEdit's <see cref="TextDocument"/>: one copy of the text and one undo history
/// for the editor, commands and the agent (ADR 0003). "Modified" follows the undo stack's saved mark.
/// </summary>
public sealed class AvalonTextBuffer : ITextBuffer
{
    /// <param name="owner">Thread that will own the document: AvalonEdit checks thread access.</param>
    public AvalonTextBuffer(string text, Thread owner)
    {
        Document = new TextDocument(text);
        Document.UndoStack.MarkAsOriginalFile();
        Document.TextChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        Document.UndoStack.PropertyChanged += OnUndoStackPropertyChanged;

        // A large file's lines are parsed in the background; from here on the document lives on the UI thread.
        if (Thread.CurrentThread != owner)
        {
            Document.SetOwnerThread(owner);
        }
    }

    /// <summary>The AvalonEdit document shown by the editor view.</summary>
    public TextDocument Document { get; }

    public int Length => Document.TextLength;

    public bool IsModified => !Document.UndoStack.IsOriginalFile;

    public bool CanUndo => Document.UndoStack.CanUndo;

    public bool CanRedo => Document.UndoStack.CanRedo;

    public event EventHandler? Changed;

    public event EventHandler? ModifiedChanged;

    public string GetText() => Document.Text;

    public string GetText(int offset, int length) => Document.GetText(offset, length);

    public void Replace(int offset, int length, string text) => Document.Replace(offset, length, text);

    public void ReplaceAll(IReadOnlyList<TextReplacement> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);

        // One undo group; applied from the end so earlier replacements don't shift later offsets.
        using (Document.RunUpdate())
        {
            foreach (var replacement in replacements.OrderByDescending(replacement => replacement.Offset))
            {
                Document.Replace(replacement.Offset, replacement.Length, replacement.Text);
            }
        }
    }

    public void Undo()
    {
        if (CanUndo)
        {
            Document.UndoStack.Undo();
        }
    }

    public void Redo()
    {
        if (CanRedo)
        {
            Document.UndoStack.Redo();
        }
    }

    public void MarkSaved() => Document.UndoStack.MarkAsOriginalFile();

    public void Reset(string text)
    {
        Document.Text = text;
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
    }

    private void OnUndoStackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UndoStack.IsOriginalFile))
        {
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
