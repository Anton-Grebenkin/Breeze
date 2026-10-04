using Shop.Domain;

namespace Shop.Application.Services;

/// <summary>Счёт по заказу: позиции и суммы.</summary>
public sealed record Invoice(Guid OrderId, IReadOnlyList<OrderLine> Lines, Money Subtotal, Money Tax, Money Total);
