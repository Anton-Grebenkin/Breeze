using System.Globalization;

namespace Shop.Domain;

/// <summary>Сумма в рублях. Единственное место округления денег — <see cref="Round"/>.</summary>
public readonly record struct Money(decimal Amount) : IComparable<Money>
{
    public static Money Zero { get; } = new(0m);

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount);

    public static Money operator *(Money money, decimal factor) => new(money.Amount * factor);

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    /// <summary>Банковское округление до копеек (к чётному).</summary>
    public Money Round() => new(Math.Round(Amount, 2, MidpointRounding.ToEven));

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public override string ToString() => Amount.ToString("0.00", CultureInfo.InvariantCulture);
}
