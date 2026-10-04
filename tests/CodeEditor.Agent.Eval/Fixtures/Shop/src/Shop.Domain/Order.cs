namespace Shop.Domain;

/// <summary>
/// Заказ: черновик собирается из позиций, затем оформляется и может быть отменён. Изменять можно только черновик.
/// </summary>
public sealed class Order(Guid id, string customerEmail)
{
    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; } = id;

    public string CustomerEmail { get; } = customerEmail;

    public OrderStatus Status { get; private set; } = OrderStatus.Draft;

    public IReadOnlyList<OrderLine> Lines => _lines;

    public void AddLine(Product product, int quantity)
    {
        EnsureDraft();
        if (quantity <= 0)
        {
            throw new DomainException("INVALID_QUANTITY", "Количество должно быть больше нуля.");
        }

        _lines.Add(new OrderLine(product.Id, product.Name, product.Price, quantity));
    }

    /// <summary>Проверка перед оформлением: черновик и хотя бы одна позиция.</summary>
    public void EnsureCanPlace()
    {
        EnsureDraft();
        if (_lines.Count == 0)
        {
            throw new DomainException("EMPTY_ORDER", "В заказе нет позиций.");
        }
    }

    public void MarkPlaced()
    {
        EnsureCanPlace();
        Status = OrderStatus.Placed;
    }

    public void MarkCancelled() => Status = OrderStatus.Cancelled;

    public void EnsureDraft()
    {
        if (Status != OrderStatus.Draft)
        {
            throw new DomainException("ORDER_NOT_DRAFT", "Изменять можно только черновик заказа.");
        }
    }
}
