using System.Windows;
using CodeEditor.Core.Documents;

namespace CodeEditor.Modules.TextEditor.Wpf.Buffers;

/// <summary><c>TextDocument</c> buffers: built on the calling thread, then handed to the app's UI thread.</summary>
public sealed class AvalonTextBufferFactory(Application application) : ITextBufferFactory
{
    public ITextBuffer Create(string text) => new AvalonTextBuffer(text, application.Dispatcher.Thread);
}
