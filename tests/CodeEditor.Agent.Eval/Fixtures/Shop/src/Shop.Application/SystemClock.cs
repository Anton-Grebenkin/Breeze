using Shop.Domain;

namespace Shop.Application;

/// <summary>Настоящее время для работающего магазина; тесты подставляют свои часы.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => TimeProvider.System.GetLocalNow();
}
