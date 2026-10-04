using CodeEditor.Core.Documents;

namespace CodeEditor.Testing;

public sealed class TestTextBufferFactory : ITextBufferFactory
{
    public ITextBuffer Create(string text) => new TestTextBuffer(text);
}
