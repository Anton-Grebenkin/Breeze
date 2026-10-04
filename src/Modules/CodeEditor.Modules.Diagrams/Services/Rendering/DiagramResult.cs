using System.Diagnostics.CodeAnalysis;

namespace CodeEditor.Modules.Diagrams.Services.Rendering;

/// <summary>
/// The result of rendering or checking a diagram: a value or an error in the diagram text. Text errors are normal while
/// a person types, so they are a result, not an exception; a failure of the renderer itself is a
/// <see cref="DiagramRendererException"/>.
/// </summary>
public sealed record DiagramResult<T>
    where T : class
{
    private DiagramResult(T? value, DiagramError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    /// <summary>The error in the diagram text; <c>null</c> if the diagram rendered.</summary>
    public DiagramError? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => Value is not null;

    public static DiagramResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DiagramResult<T>(value, null);
    }

    public static DiagramResult<T> Failure(DiagramError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DiagramResult<T>(null, error);
    }
}
