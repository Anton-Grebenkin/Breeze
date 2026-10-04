using Shop.Application.Repositories;
using Shop.Domain;

namespace Shop.Application.Services;

/// <summary>Сценарии заказа: создать, добавить товар, оформить, отменить.</summary>
public sealed class OrderService(IOrderRepository orders, IProductRepository products, InventoryService inventory, NotificationService notifications)
{
    public Order Create(string customerEmail)
    {
        var order = new Order(Guid.NewGuid(), customerEmail);
        orders.Add(order);
        return order;
    }

    public Order Get(Guid orderId) =>
        orders.Find(orderId) ?? throw new DomainException("ORDER_NOT_FOUND", $"Заказа {orderId} нет.");

    public void AddItem(Guid orderId, Guid productId, int quantity)
    {
        var product = products.Find(productId) ?? throw new DomainException("PRODUCT_NOT_FOUND", $"Товара {productId} нет.");
        Get(orderId).AddLine(product, quantity);
    }

    public void Place(Guid orderId)
    {
        var order = Get(orderId);
        order.EnsureCanPlace();
        inventory.Reserve(order.Lines);
        order.MarkPlaced();
        notifications.OrderPlaced(order);
    }

    /// <summary>Отмена: оформленный заказ возвращает товар на склад, черновик склад не трогает, повтор — ничего не делает.</summary>
    public void Cancel(Guid orderId)
    {
        var order = Get(orderId);
        if (order.Status == OrderStatus.Cancelled)
        {
            return;
        }

        if (order.Status == OrderStatus.Placed)
        {
            inventory.Release(order.Lines);
        }

        order.MarkCancelled();
        notifications.OrderCancelled(order);
    }
}
