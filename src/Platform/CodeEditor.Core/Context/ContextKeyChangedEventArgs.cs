namespace CodeEditor.Core.Context;

public sealed class ContextKeyChangedEventArgs(string key) : EventArgs
{
    public string Key { get; } = key;
}
