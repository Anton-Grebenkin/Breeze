using Shop.Domain;

namespace Shop.Application.Services;

/// <summary>Цена заказа — единственное место расчёта: сумма позиций, НДС 20 % и итог.</summary>
public sealed class PricingService
{
    public const decimal VatRate = 0.20m;

    public Money Subtotal(Order order) => order.Lines.Aggregate(Money.Zero, (sum, line) => sum + line.Total);

    public Money Tax(Order order) => (Subtotal(order) * VatRate).Round();

    public Money Total(Order order) => Subtotal(order) + Tax(order);
}
