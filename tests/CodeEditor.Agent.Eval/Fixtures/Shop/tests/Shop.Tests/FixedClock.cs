using Shop.Domain;

namespace Shop.Tests;

/// <summary>Часы, которые тест переставляет сам.</summary>
public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now { get; set; } = now;
}
