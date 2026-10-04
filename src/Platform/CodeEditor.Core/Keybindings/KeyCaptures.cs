using CodeEditor.Core.Context;

namespace CodeEditor.Core.Keybindings;

/// <summary>The registered <see cref="KeyCapture"/>s; the resolver asks which one is active for the current context.</summary>
public sealed class KeyCaptures
{
    private readonly List<KeyCapture> _captures = [];

    public IDisposable Register(KeyCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        _captures.Add(capture);
        return new Registration(this, capture);
    }

    /// <summary>The capture whose context key is set; <c>null</c> when keys go the usual way.</summary>
    public KeyCapture? Active(IContextKeyLookup context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _captures.Find(capture => context.GetValue(capture.ContextKey) is true);
    }

    private sealed class Registration(KeyCaptures owner, KeyCapture capture) : IDisposable
    {
        public void Dispose() => owner._captures.Remove(capture);
    }
}
