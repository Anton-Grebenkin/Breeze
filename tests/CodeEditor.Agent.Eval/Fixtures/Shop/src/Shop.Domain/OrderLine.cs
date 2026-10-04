namespace Shop.Domain;

/// <summary>Позиция заказа: цена фиксируется в момент добавления.</summary>
public sealed record OrderLine(Guid ProductId, string ProductName, Money UnitPrice, int Quantity)
{
    public Money Total => UnitPrice * Quantity;
}
