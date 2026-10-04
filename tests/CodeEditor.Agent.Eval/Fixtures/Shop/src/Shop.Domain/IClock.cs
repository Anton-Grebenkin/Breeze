namespace Shop.Domain;

/// <summary>Текущее время. В коде время берётся только отсюда — так его можно подменить в тестах.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}
